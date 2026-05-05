using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RepairPlanner.Models;

// A single ordered step inside a WorkOrder.
public sealed class RepairTask
{
    [System.Text.Json.Serialization.JsonPropertyName("sequence")]
    [Newtonsoft.Json.JsonProperty("sequence")]
    public int Sequence { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("title")]
    [Newtonsoft.Json.JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("description")]
    [Newtonsoft.Json.JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    // Always an integer count of minutes (e.g. 30, not "30 min").
    [System.Text.Json.Serialization.JsonPropertyName("estimatedDurationMinutes")]
    [Newtonsoft.Json.JsonProperty("estimatedDurationMinutes")]
    public int EstimatedDurationMinutes { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("requiredSkills")]
    [Newtonsoft.Json.JsonProperty("requiredSkills")]
    public List<string> RequiredSkills { get; set; } = new();

    [System.Text.Json.Serialization.JsonPropertyName("safetyNotes")]
    [Newtonsoft.Json.JsonProperty("safetyNotes")]
    public string? SafetyNotes { get; set; }
}
