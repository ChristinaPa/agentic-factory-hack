using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RepairPlanner.Models;

// An inventory item that may be required for a repair.
// Stored in the "PartsInventory" Cosmos container (partition key: category).
public sealed class Part
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    [Newtonsoft.Json.JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    // Human-friendly part number, e.g. "TCP-HTR-4KW".
    [System.Text.Json.Serialization.JsonPropertyName("partNumber")]
    [Newtonsoft.Json.JsonProperty("partNumber")]
    public string PartNumber { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("name")]
    [Newtonsoft.Json.JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("description")]
    [Newtonsoft.Json.JsonProperty("description")]
    public string? Description { get; set; }

    // Partition key.
    [System.Text.Json.Serialization.JsonPropertyName("category")]
    [Newtonsoft.Json.JsonProperty("category")]
    public string Category { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("quantityOnHand")]
    [Newtonsoft.Json.JsonProperty("quantityOnHand")]
    public int QuantityOnHand { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("reorderLevel")]
    [Newtonsoft.Json.JsonProperty("reorderLevel")]
    public int ReorderLevel { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("unitCost")]
    [Newtonsoft.Json.JsonProperty("unitCost")]
    public decimal? UnitCost { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("supplierId")]
    [Newtonsoft.Json.JsonProperty("supplierId")]
    public string? SupplierId { get; set; }
}
