using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BBF.Services;

public class HomeAssistantService(HttpClient http, IConfiguration config)
{
    private readonly string _baseUrl = config["HomeAssistant:Url"] ?? "http://10.69.1.153:8123";
    private readonly string _token = config["HomeAssistant:Token"] ?? "";

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, $"{_baseUrl}{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        if (body is not null)
            request.Content = JsonContent.Create(body);

        return request;
    }

    public async Task<List<HaEntity>> GetEntitiesAsync(CancellationToken ct = default)
    {
        try
        {
            var request = CreateRequest(HttpMethod.Get, "/api/states");
            var response = await http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<List<HaEntity>>(ct) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<List<HaEntity>> GetEntitiesByDomainAsync(string domain, CancellationToken ct = default)
    {
        var all = await GetEntitiesAsync(ct);
        return all.Where(e => e.EntityId.StartsWith($"{domain}.")).ToList();
    }

    public async Task<HaEntity?> GetEntityAsync(string entityId, CancellationToken ct = default)
    {
        try
        {
            var request = CreateRequest(HttpMethod.Get, $"/api/states/{entityId}");
            var response = await http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<HaEntity>(ct);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> CallServiceAsync(string domain, string service, string entityId, object? data = null, CancellationToken ct = default)
    {
        try
        {
            var payload = new Dictionary<string, object> { ["entity_id"] = entityId };

            if (data is IDictionary<string, object> extra)
            {
                foreach (var kv in extra)
                    payload[kv.Key] = kv.Value;
            }

            var request = CreateRequest(HttpMethod.Post, $"/api/services/{domain}/{service}", payload);
            var response = await http.SendAsync(request, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public Task<bool> TurnOnAsync(string entityId, CancellationToken ct = default)
    {
        var domain = entityId.Split('.')[0];
        return CallServiceAsync(domain, "turn_on", entityId, ct: ct);
    }

    public Task<bool> TurnOffAsync(string entityId, CancellationToken ct = default)
    {
        var domain = entityId.Split('.')[0];
        return CallServiceAsync(domain, "turn_off", entityId, ct: ct);
    }

    public Task<bool> ToggleAsync(string entityId, CancellationToken ct = default)
    {
        var domain = entityId.Split('.')[0];
        return CallServiceAsync(domain, "toggle", entityId, ct: ct);
    }

    public Task<bool> SetTemperatureAsync(string entityId, double temperature, CancellationToken ct = default)
    {
        return CallServiceAsync("climate", "set_temperature", entityId,
            new Dictionary<string, object> { ["temperature"] = temperature }, ct);
    }

    public Task<bool> SetHvacModeAsync(string entityId, string mode, CancellationToken ct = default)
    {
        return CallServiceAsync("climate", "set_hvac_mode", entityId,
            new Dictionary<string, object> { ["hvac_mode"] = mode }, ct);
    }

    public Task<bool> SetFanSpeedAsync(string entityId, string percentage, CancellationToken ct = default)
    {
        return CallServiceAsync("fan", "set_percentage", entityId,
            new Dictionary<string, object> { ["percentage"] = int.Parse(percentage) }, ct);
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var request = CreateRequest(HttpMethod.Get, "/api/");
            var response = await http.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}

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
