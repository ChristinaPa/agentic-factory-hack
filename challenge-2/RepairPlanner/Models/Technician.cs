using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RepairPlanner.Models;

// A maintenance technician available for assignment.
// Stored in the "Technicians" Cosmos container (partition key: department).
public sealed class Technician
{
    // Cosmos requires lowercase "id" -> use both attribute styles.
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    [Newtonsoft.Json.JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("name")]
    [Newtonsoft.Json.JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    // Partition key.
    [System.Text.Json.Serialization.JsonPropertyName("department")]
    [Newtonsoft.Json.JsonProperty("department")]
    public string Department { get; set; } = string.Empty;

    // Skill identifiers, e.g. "tire_curing_press", "plc_troubleshooting".
    [System.Text.Json.Serialization.JsonPropertyName("skills")]
    [Newtonsoft.Json.JsonProperty("skills")]
    public List<string> Skills { get; set; } = new();

    // "available" | "busy" | "off_shift" | "on_leave"
    [System.Text.Json.Serialization.JsonPropertyName("availability")]
    [Newtonsoft.Json.JsonProperty("availability")]
    public string Availability { get; set; } = "available";

    [System.Text.Json.Serialization.JsonPropertyName("shift")]
    [Newtonsoft.Json.JsonProperty("shift")]
    public string? Shift { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("certificationLevel")]
    [Newtonsoft.Json.JsonProperty("certificationLevel")]
    public string? CertificationLevel { get; set; }
}
