namespace RepairPlanner.Services;

// Configuration for the Cosmos DB connection.
// Values come from environment variables in Program.cs.
public sealed class CosmosDbOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;

    // Container names (override only if your seed data uses different ones).
    public string TechniciansContainer { get; set; } = "Technicians";
    public string PartsContainer { get; set; } = "PartsInventory";
    public string WorkOrdersContainer { get; set; } = "WorkOrders";
}
