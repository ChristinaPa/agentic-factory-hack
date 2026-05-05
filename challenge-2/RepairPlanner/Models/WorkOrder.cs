using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RepairPlanner.Models;

// Output model: the repair plan produced by the agent.
// Stored in the "WorkOrders" Cosmos container (partition key: status).
public sealed class WorkOrder
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    [Newtonsoft.Json.JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [System.Text.Json.Serialization.JsonPropertyName("workOrderNumber")]
    [Newtonsoft.Json.JsonProperty("workOrderNumber")]
    public string WorkOrderNumber { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("machineId")]
    [Newtonsoft.Json.JsonProperty("machineId")]
    public string MachineId { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("title")]
    [Newtonsoft.Json.JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("description")]
    [Newtonsoft.Json.JsonProperty("description")]
    public string? Description { get; set; }

    // "corrective" | "preventive" | "emergency"
    [System.Text.Json.Serialization.JsonPropertyName("type")]
    [Newtonsoft.Json.JsonProperty("type")]
    public string? Type { get; set; }

    // "critical" | "high" | "medium" | "low"
    [System.Text.Json.Serialization.JsonPropertyName("priority")]
    [Newtonsoft.Json.JsonProperty("priority")]
    public string? Priority { get; set; }

    // Partition key.  "open" | "scheduled" | "in_progress" | "completed" | "cancelled"
    [System.Text.Json.Serialization.JsonPropertyName("status")]
    [Newtonsoft.Json.JsonProperty("status")]
    public string Status { get; set; } = "open";

    // Technician id (or null if no qualified technician was found).
    [System.Text.Json.Serialization.JsonPropertyName("assignedTo")]
    [Newtonsoft.Json.JsonProperty("assignedTo")]
    public string? AssignedTo { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("notes")]
    [Newtonsoft.Json.JsonProperty("notes")]
    public string? Notes { get; set; }

    // Total minutes for the whole job (integer).
    [System.Text.Json.Serialization.JsonPropertyName("estimatedDuration")]
    [Newtonsoft.Json.JsonProperty("estimatedDuration")]
    public int EstimatedDuration { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("partsUsed")]
    [Newtonsoft.Json.JsonProperty("partsUsed")]
    public List<WorkOrderPartUsage> PartsUsed { get; set; } = new();

    [System.Text.Json.Serialization.JsonPropertyName("tasks")]
    [Newtonsoft.Json.JsonProperty("tasks")]
    public List<RepairTask> Tasks { get; set; } = new();

    [System.Text.Json.Serialization.JsonPropertyName("createdAt")]
    [Newtonsoft.Json.JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
