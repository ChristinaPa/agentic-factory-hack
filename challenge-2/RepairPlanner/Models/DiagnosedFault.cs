using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RepairPlanner.Models;

// Input model: produced by the Fault Diagnosis Agent (Challenge 1).
// Dual JSON attributes:
//   - [JsonPropertyName] -> System.Text.Json (LLM JSON round-trip)
//   - [JsonProperty]     -> Newtonsoft.Json  (Cosmos DB SDK serializer)
public sealed class DiagnosedFault
{
    [System.Text.Json.Serialization.JsonPropertyName("machineId")]
    [Newtonsoft.Json.JsonProperty("machineId")]
    public string MachineId { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("machineType")]
    [Newtonsoft.Json.JsonProperty("machineType")]
    public string MachineType { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("faultType")]
    [Newtonsoft.Json.JsonProperty("faultType")]
    public string FaultType { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("rootCause")]
    [Newtonsoft.Json.JsonProperty("rootCause")]
    public string? RootCause { get; set; }

    // "Low" | "Medium" | "High" | "Critical" | "Unknown"
    [System.Text.Json.Serialization.JsonPropertyName("severity")]
    [Newtonsoft.Json.JsonProperty("severity")]
    public string? Severity { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("detectedAt")]
    [Newtonsoft.Json.JsonProperty("detectedAt")]
    public DateTime? DetectedAt { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("description")]
    [Newtonsoft.Json.JsonProperty("description")]
    public string? Description { get; set; }

    // Free-form metadata from the diagnosis agent (observed values, KB refs, etc.)
    [System.Text.Json.Serialization.JsonPropertyName("metadata")]
    [Newtonsoft.Json.JsonProperty("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}
