using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RepairPlanner.Models;

// A part allocated to a WorkOrder.
public sealed class WorkOrderPartUsage
{
    // Cosmos document id of the Part (if known).
    [System.Text.Json.Serialization.JsonPropertyName("partId")]
    [Newtonsoft.Json.JsonProperty("partId")]
    public string? PartId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("partNumber")]
    [Newtonsoft.Json.JsonProperty("partNumber")]
    public string PartNumber { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("quantity")]
    [Newtonsoft.Json.JsonProperty("quantity")]
    public int Quantity { get; set; } = 1;
}
