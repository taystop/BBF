using System.Text.Json;
using System.Text.Json.Serialization;

namespace BBF.Shared.Contracts.HomeAssistant;

public class HaEntity
{
    [JsonPropertyName("entity_id")]
    public string EntityId { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("attributes")]
    public JsonElement Attributes { get; set; }

    [JsonPropertyName("last_changed")]
    public DateTime? LastChanged { get; set; }

    [JsonPropertyName("last_updated")]
    public DateTime? LastUpdated { get; set; }

    public string Domain => EntityId.Split('.')[0];

    public string FriendlyName =>
        Attributes.TryGetProperty("friendly_name", out var name) ? name.GetString() ?? EntityId : EntityId;

    public string? Icon =>
        Attributes.TryGetProperty("icon", out var icon) ? icon.GetString() : null;

    public string? UnitOfMeasurement =>
        Attributes.TryGetProperty("unit_of_measurement", out var unit) ? unit.GetString() : null;

    public double? CurrentTemperature =>
        Attributes.TryGetProperty("current_temperature", out var temp) && temp.ValueKind == JsonValueKind.Number
            ? temp.GetDouble() : null;

    public double? TargetTemperature =>
        Attributes.TryGetProperty("temperature", out var temp) && temp.ValueKind == JsonValueKind.Number
            ? temp.GetDouble() : null;

    public string? HvacMode =>
        Attributes.TryGetProperty("hvac_mode", out var mode) ? mode.GetString() : null;

    public List<string> HvacModes
    {
        get
        {
            if (Attributes.TryGetProperty("hvac_modes", out var modes) && modes.ValueKind == JsonValueKind.Array)
                return modes.EnumerateArray().Select(m => m.GetString() ?? "").Where(m => m != "").ToList();
            return [];
        }
    }

    public int? FanPercentage =>
        Attributes.TryGetProperty("percentage", out var pct) && pct.ValueKind == JsonValueKind.Number
            ? pct.GetInt32() : null;
}

public record HaServiceCallRequest(string Domain, string Service, string EntityId, Dictionary<string, object>? Data = null);
