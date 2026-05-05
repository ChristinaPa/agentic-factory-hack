using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RepairPlanner.Models;

namespace RepairPlanner.Services;

// Thin Cosmos DB data-access layer for the Repair Planner Agent.
//
// Containers (partition keys):
//   - Technicians     (PK: department)
//   - PartsInventory  (PK: category)
//   - WorkOrders      (PK: status)
//
// Note: cross-partition queries are used here because we don't know the
// department/category up front. That's fine for the workshop scale.
//
// Primary constructor pattern (C# 12) - parameters become private fields,
// similar to Python's __init__ assigning to self.
public sealed class CosmosDbService(
    CosmosClient cosmosClient,
    CosmosDbOptions options,
    ILogger<CosmosDbService> logger)
{
    private Container Technicians => cosmosClient
        .GetDatabase(options.DatabaseName)
        .GetContainer(options.TechniciansContainer);

    private Container Parts => cosmosClient
        .GetDatabase(options.DatabaseName)
        .GetContainer(options.PartsContainer);

    private Container WorkOrders => cosmosClient
        .GetDatabase(options.DatabaseName)
        .GetContainer(options.WorkOrdersContainer);

    // Returns available technicians whose skill set intersects requiredSkills.
    public async Task<IReadOnlyList<Technician>> GetAvailableTechniciansAsync(
        IReadOnlyList<string> requiredSkills,
        CancellationToken ct = default)
    {
        if (requiredSkills is null || requiredSkills.Count == 0)
        {
            logger.LogWarning("GetAvailableTechniciansAsync called with no required skills.");
            return Array.Empty<Technician>();
        }

        // Cosmos SQL: ARRAY_CONTAINS(c.skills, @skill) for any of the required skills.
        // We build (ARRAY_CONTAINS(c.skills, @s0) OR ARRAY_CONTAINS(c.skills, @s1) ...).
        var skillPredicates = string.Join(
            " OR ",
            requiredSkills.Select((_, i) => $"ARRAY_CONTAINS(c.skills, @s{i})"));

        var sql =
            $"SELECT * FROM c WHERE c.availability = 'available' AND ({skillPredicates})";

        var query = new QueryDefinition(sql);
        for (var i = 0; i < requiredSkills.Count; i++)
        {
            query = query.WithParameter($"@s{i}", requiredSkills[i]);
        }

        var results = new List<Technician>();
        try
        {
            // FeedIterator is IDisposable (not IAsyncDisposable) - use plain `using`.
            using var iterator = Technicians.GetItemQueryIterator<Technician>(query);
            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync(ct);
                results.AddRange(page);
            }

            logger.LogInformation(
                "Found {Count} available technician(s) for skills [{Skills}].",
                results.Count, string.Join(", ", requiredSkills));
            return results;
        }
        catch (CosmosException ex)
        {
            logger.LogError(ex,
                "Cosmos error querying technicians (status {Status}).", ex.StatusCode);
            return Array.Empty<Technician>();
        }
    }

    // Returns inventory parts matching the given part numbers.
    public async Task<IReadOnlyList<Part>> GetPartsByNumbersAsync(
        IReadOnlyList<string> partNumbers,
        CancellationToken ct = default)
    {
        if (partNumbers is null || partNumbers.Count == 0)
        {
            return Array.Empty<Part>();
        }

        var sql = "SELECT * FROM c WHERE ARRAY_CONTAINS(@partNumbers, c.partNumber)";
        var query = new QueryDefinition(sql)
            .WithParameter("@partNumbers", partNumbers);

        var results = new List<Part>();
        try
        {
            using var iterator = Parts.GetItemQueryIterator<Part>(query);
            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync(ct);
                results.AddRange(page);
            }

            logger.LogInformation(
                "Found {Count} part(s) for [{PartNumbers}].",
                results.Count, string.Join(", ", partNumbers));
            return results;
        }
        catch (CosmosException ex)
        {
            logger.LogError(ex,
                "Cosmos error querying parts (status {Status}).", ex.StatusCode);
            return Array.Empty<Part>();
        }
    }

    // Persists a WorkOrder. The container is partitioned by "status".
    public async Task<WorkOrder> CreateWorkOrderAsync(
        WorkOrder workOrder,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workOrder);

        // ??= means "assign if null" (like Python's: x = x or default_value)
        workOrder.Id ??= string.Empty;
        if (string.IsNullOrWhiteSpace(workOrder.Id))
        {
            workOrder.Id = Guid.NewGuid().ToString();
        }
        workOrder.Status ??= "open";

        try
        {
            var response = await WorkOrders.CreateItemAsync(
                workOrder,
                new PartitionKey(workOrder.Status),
                cancellationToken: ct);

            logger.LogInformation(
                "Created work order {Id} (status={Status}) for machine {MachineId}. RU charge: {RU}",
                response.Resource.Id, response.Resource.Status,
                response.Resource.MachineId, response.RequestCharge);

            return response.Resource;
        }
        catch (CosmosException ex)
        {
            logger.LogError(ex,
                "Cosmos error creating work order {Id} (status {Status}).",
                workOrder.Id, ex.StatusCode);
            throw;
        }
    }
}
