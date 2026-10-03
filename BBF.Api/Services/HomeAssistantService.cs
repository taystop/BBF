using System.Net.Http.Headers;
using System.Net.Http.Json;
using BBF.Shared.Contracts.HomeAssistant;

namespace BBF.Api.Services;

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

    public async Task<bool> CallServiceAsync(HaServiceCallRequest call, CancellationToken ct = default)
    {
        try
        {
            var payload = new Dictionary<string, object> { ["entity_id"] = call.EntityId };
            if (call.Data is not null)
            {
                foreach (var kv in call.Data)
                    payload[kv.Key] = kv.Value;
            }

            var request = CreateRequest(HttpMethod.Post, $"/api/services/{call.Domain}/{call.Service}", payload);
            var response = await http.SendAsync(request, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public Task<bool> TurnOnAsync(string entityId, CancellationToken ct = default) =>
        CallServiceAsync(new HaServiceCallRequest(entityId.Split('.')[0], "turn_on", entityId), ct);

    public Task<bool> TurnOffAsync(string entityId, CancellationToken ct = default) =>
        CallServiceAsync(new HaServiceCallRequest(entityId.Split('.')[0], "turn_off", entityId), ct);

    public Task<bool> ToggleAsync(string entityId, CancellationToken ct = default) =>
        CallServiceAsync(new HaServiceCallRequest(entityId.Split('.')[0], "toggle", entityId), ct);

    public Task<bool> SetTemperatureAsync(string entityId, double temperature, CancellationToken ct = default) =>
        CallServiceAsync(new HaServiceCallRequest("climate", "set_temperature", entityId,
            new Dictionary<string, object> { ["temperature"] = temperature }), ct);

    public Task<bool> SetHvacModeAsync(string entityId, string mode, CancellationToken ct = default) =>
        CallServiceAsync(new HaServiceCallRequest("climate", "set_hvac_mode", entityId,
            new Dictionary<string, object> { ["hvac_mode"] = mode }), ct);

    public Task<bool> SetFanSpeedAsync(string entityId, string percentage, CancellationToken ct = default) =>
        CallServiceAsync(new HaServiceCallRequest("fan", "set_percentage", entityId,
            new Dictionary<string, object> { ["percentage"] = int.Parse(percentage) }), ct);

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            var request = CreateRequest(HttpMethod.Get, "/api/");
            var response = await http.SendAsync(request, ct);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}
