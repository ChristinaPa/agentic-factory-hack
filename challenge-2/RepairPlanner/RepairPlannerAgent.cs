using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using RepairPlanner.Models;
using RepairPlanner.Services;

namespace RepairPlanner;

// Orchestrates the repair planning workflow:
//   1. Look up required skills + parts for the diagnosed fault.
//   2. Query Cosmos DB for available technicians and matching inventory parts.
//   3. Ask a Foundry Prompt Agent to produce a WorkOrder (JSON).
//   4. Apply safe defaults to the agent's response.
//   5. Persist the WorkOrder back to Cosmos DB.
//
// Uses the Foundry Agents SDK pattern (NOT direct ChatCompletions):
//   - AIProjectClient + PromptAgentDefinition + CreateAgentVersionAsync
//   - projectClient.GetAIAgent(name).RunAsync(...)
public sealed class RepairPlannerAgent(
    AIProjectClient projectClient,
    CosmosDbService cosmosDb,
    IFaultMappingService faultMapping,
    string modelDeploymentName,
    ILogger<RepairPlannerAgent> logger)
{
    private const string AgentName = "RepairPlannerAgent";

    private const string AgentInstructions = """
        You are a Repair Planner Agent for tire manufacturing equipment.
        Generate a repair plan with tasks, timeline, and resource allocation.
        Return the response as valid JSON matching the WorkOrder schema.

        Output JSON with these fields:
        - workOrderNumber, machineId, title, description
        - type: "corrective" | "preventive" | "emergency"
        - priority: "critical" | "high" | "medium" | "low"
        - status, assignedTo (technician id or null), notes
        - estimatedDuration: integer (minutes, e.g. 60 not "60 minutes")
        - partsUsed: [{ partId, partNumber, quantity }]
        - tasks: [{ sequence, title, description, estimatedDurationMinutes (integer), requiredSkills, safetyNotes }]

        IMPORTANT: All duration fields must be integers representing minutes (e.g. 90), not strings.

        Rules:
        - Assign the most qualified available technician (use the technician id from the candidate list).
        - Include only relevant parts; empty array if none needed.
        - Tasks must be ordered (sequence starting at 1) and actionable.
        - Output ONLY the JSON object, no markdown, no commentary.
        """;

    // LLMs sometimes emit numbers as JSON strings ("60" instead of 60).
    // AllowReadingFromString lets us parse both.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    // Register / update the agent definition in the Foundry project.
    // Call once at startup before invoking the agent.
    public async Task EnsureAgentVersionAsync(CancellationToken ct = default)
    {
        var definition = new PromptAgentDefinition(model: modelDeploymentName)
        {
            Instructions = AgentInstructions,
        };

        await projectClient.Agents.CreateAgentVersionAsync(
            AgentName,
            new AgentVersionCreationOptions(definition),
            ct);

        logger.LogInformation("Registered Foundry agent '{Agent}' (model {Model}).",
            AgentName, modelDeploymentName);
    }

    // Main entry point: turn a DiagnosedFault into a persisted WorkOrder.
    public async Task<WorkOrder> PlanAndCreateWorkOrderAsync(
        DiagnosedFault fault,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(fault);

        logger.LogInformation(
            "Planning repair for machine {MachineId} (fault: {FaultType}).",
            fault.MachineId, fault.FaultType);

        // 1. Mappings (deterministic, hardcoded).
        var requiredSkills = faultMapping.GetRequiredSkills(fault.FaultType);
        var requiredParts = faultMapping.GetRequiredParts(fault.FaultType);

        // 2. Ground the LLM with real Cosmos data (run in parallel).
        var techniciansTask = cosmosDb.GetAvailableTechniciansAsync(requiredSkills, ct);
        var partsTask = cosmosDb.GetPartsByNumbersAsync(requiredParts, ct);
        await Task.WhenAll(techniciansTask, partsTask);

        var technicians = await techniciansTask;
        var parts = await partsTask;

        // 3. Invoke the Foundry agent.
        var prompt = BuildPrompt(fault, requiredSkills, requiredParts, technicians, parts);
        var agent = projectClient.GetAIAgent(name: AgentName);
        var response = await agent.RunAsync(prompt, thread: null, options: null, cancellationToken: ct);
        var rawText = response.Text ?? string.Empty;

        logger.LogDebug("Agent response ({Length} chars): {Text}", rawText.Length, rawText);

        // 4. Parse + apply defaults.
        var workOrder = ParseWorkOrder(rawText, fault);
        ApplyDefaults(workOrder, fault, technicians);

        // 5. Persist.
        return await cosmosDb.CreateWorkOrderAsync(workOrder, ct);
    }

    private static string BuildPrompt(
        DiagnosedFault fault,
        IReadOnlyList<string> requiredSkills,
        IReadOnlyList<string> requiredParts,
        IReadOnlyList<Technician> technicians,
        IReadOnlyList<Part> parts)
    {
        // Serialize the candidate lists so the LLM sees structured data.
        var faultJson = JsonSerializer.Serialize(fault, JsonOptions);
        var techniciansJson = JsonSerializer.Serialize(
            technicians.Select(t => new
            {
                id = t.Id,
                name = t.Name,
                skills = t.Skills,
                certificationLevel = t.CertificationLevel,
                shift = t.Shift,
            }),
            JsonOptions);
        var partsJson = JsonSerializer.Serialize(
            parts.Select(p => new
            {
                partId = p.Id,
                partNumber = p.PartNumber,
                name = p.Name,
                quantityOnHand = p.QuantityOnHand,
            }),
            JsonOptions);

        var sb = new StringBuilder();
        sb.AppendLine("Plan a repair work order for the following diagnosed fault.");
        sb.AppendLine();
        sb.AppendLine("DIAGNOSED FAULT:");
        sb.AppendLine(faultJson);
        sb.AppendLine();
        sb.AppendLine($"REQUIRED SKILLS: [{string.Join(", ", requiredSkills)}]");
        sb.AppendLine($"SUGGESTED PART NUMBERS: [{string.Join(", ", requiredParts)}]");
        sb.AppendLine();
        sb.AppendLine("AVAILABLE TECHNICIANS (pick the most qualified, use their id for assignedTo):");
        sb.AppendLine(techniciansJson);
        sb.AppendLine();
        sb.AppendLine("PARTS IN INVENTORY (use these for partsUsed entries):");
        sb.AppendLine(partsJson);
        sb.AppendLine();
        sb.AppendLine("Respond with the WorkOrder JSON only.");
        return sb.ToString();
    }

    private WorkOrder ParseWorkOrder(string rawText, DiagnosedFault fault)
    {
        // The model is told to return JSON only, but be defensive: extract the
        // first {...} block in case it wraps the JSON in prose or code fences.
        var json = ExtractJsonObject(rawText);
        if (string.IsNullOrWhiteSpace(json))
        {
            logger.LogError("Agent response did not contain a JSON object. Raw text: {Raw}", rawText);
            throw new InvalidOperationException(
                "Repair Planner Agent did not return a JSON work order.");
        }

        try
        {
            var wo = JsonSerializer.Deserialize<WorkOrder>(json, JsonOptions);
            if (wo is null)
            {
                throw new InvalidOperationException("Deserialized WorkOrder was null.");
            }
            return wo;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex,
                "Failed to deserialize agent JSON for machine {MachineId}. JSON: {Json}",
                fault.MachineId, json);
            throw;
        }
    }

    private static string ExtractJsonObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return string.Empty;
        }

        return text.Substring(start, end - start + 1);
    }

    private static void ApplyDefaults(
        WorkOrder wo,
        DiagnosedFault fault,
        IReadOnlyList<Technician> technicians)
    {
        // ??= means "assign if null" (like Python's: x = x or default_value)
        wo.MachineId = string.IsNullOrWhiteSpace(wo.MachineId) ? fault.MachineId : wo.MachineId;
        wo.WorkOrderNumber = string.IsNullOrWhiteSpace(wo.WorkOrderNumber)
            ? $"WO-{DateTime.UtcNow:yyyyMMddHHmmss}"
            : wo.WorkOrderNumber;
        wo.Title = string.IsNullOrWhiteSpace(wo.Title)
            ? $"Repair: {fault.FaultType}"
            : wo.Title;
        wo.Type ??= "corrective";
        wo.Priority ??= MapSeverityToPriority(fault.Severity);
        wo.Status = string.IsNullOrWhiteSpace(wo.Status) ? "open" : wo.Status;

        // If the LLM didn't pick a technician but candidates exist, default to the first one.
        if (string.IsNullOrWhiteSpace(wo.AssignedTo) && technicians.Count > 0)
        {
            wo.AssignedTo = technicians[0].Id;
        }

        // Estimate duration as sum of task durations if not provided.
        if (wo.EstimatedDuration <= 0 && wo.Tasks.Count > 0)
        {
            wo.EstimatedDuration = wo.Tasks.Sum(t => t.EstimatedDurationMinutes);
        }

        if (wo.CreatedAt == default)
        {
            wo.CreatedAt = DateTime.UtcNow;
        }
    }

    private static string MapSeverityToPriority(string? severity) => severity?.ToLowerInvariant() switch
    {
        "critical" => "critical",
        "high"     => "high",
        "medium"   => "medium",
        "low"      => "low",
        _          => "medium",
    };
}
