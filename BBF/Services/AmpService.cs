using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BBF.Services;

public class AmpService(HttpClient http, IConfiguration config)
{
    private readonly string _baseUrl = config["AMP:Url"] ?? "http://10.69.1.3:8080";
    private readonly string _username = config["AMP:Username"] ?? "";
    private readonly string _password = config["AMP:Password"] ?? "";

    private string? _sessionId;
    private bool _headersSet;

    private void EnsureHeaders()
    {
        if (_headersSet) return;
        http.DefaultRequestHeaders.Accept.Clear();
        http.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        _headersSet = true;
    }

    private async Task EnsureLoggedInAsync(CancellationToken ct = default)
    {
        if (_sessionId is not null) return;
        EnsureHeaders();

        var response = await http.PostAsJsonAsync($"{_baseUrl}/API/Core/Login", new
        {
            username = _username,
            password = _password,
            token = "",
            rememberMe = false
        }, ct);

        var result = await response.Content.ReadFromJsonAsync<AmpLoginResponse>(ct);
        if (result?.success == true)
            _sessionId = result.sessionID;
        else
            throw new Exception("AMP login failed");
    }

    private async Task<JsonElement?> CallApiAsync(string endpoint, object? parameters = null, string? instanceId = null, CancellationToken ct = default)
    {
        await EnsureLoggedInAsync(ct);

        var url = instanceId is not null
            ? $"{_baseUrl}/API/ADSModule/Servers/{instanceId}{endpoint}"
            : $"{_baseUrl}/API{endpoint}";

        var body = new Dictionary<string, object?>
        {
            ["SESSIONID"] = _sessionId
        };

        if (parameters is IDictionary<string, object?> dict)
        {
            foreach (var kv in dict)
                body[kv.Key] = kv.Value;
        }

        var response = await http.PostAsJsonAsync(url, body, ct);

        if (!response.IsSuccessStatusCode)
        {
            // Session may have expired, retry once
            _sessionId = null;
            await EnsureLoggedInAsync(ct);
            body["SESSIONID"] = _sessionId;
            response = await http.PostAsJsonAsync(url, body, ct);
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(ct);
    }

    public async Task<List<AmpInstance>> GetInstancesAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await CallApiAsync("/ADSModule/GetInstances", ct: ct);
            if (result is null) return [];

            var instances = new List<AmpInstance>();

            // ADS returns array of targets, each with their own instances
            if (result.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var target in result.Value.EnumerateArray())
                {
                    if (!target.TryGetProperty("AvailableInstances", out var available)) continue;

                    foreach (var inst in available.EnumerateArray())
                    {
                        var instance = ParseInstance(inst);
                        if (instance is not null)
                            instances.Add(instance);
                    }
                }
            }

            return instances;
        }
        catch { return []; }
    }

    public async Task<AmpInstanceStatus?> GetInstanceStatusAsync(string instanceId, CancellationToken ct = default)
    {
        try
        {
            var result = await CallApiAsync("/Core/GetStatus", instanceId: instanceId, ct: ct);
            if (result is null) return null;

            return new AmpInstanceStatus
            {
                State = result.Value.TryGetProperty("State", out var state) ? state.GetInt32() : 0,
                Uptime = result.Value.TryGetProperty("Uptime", out var uptime) ? uptime.GetString() : null,
                Cpu = result.Value.TryGetProperty("Metrics", out var metrics)
                    && metrics.TryGetProperty("CPU Usage", out var cpu)
                    && cpu.TryGetProperty("Percent", out var pct)
                    ? pct.GetDouble() : null,
                Memory = metrics.ValueKind != JsonValueKind.Undefined
                    && metrics.TryGetProperty("Memory Usage", out var mem)
                    && mem.TryGetProperty("Percent", out var memPct)
                    ? memPct.GetDouble() : null,
                ActiveUsers = result.Value.TryGetProperty("Metrics", out var m2)
                    && m2.TryGetProperty("Active Users", out var users)
                    && users.TryGetProperty("RawValue", out var raw)
                    ? raw.GetInt32() : 0,
                MaxUsers = result.Value.TryGetProperty("Metrics", out var m3)
                    && m3.TryGetProperty("Active Users", out var users2)
                    && users2.TryGetProperty("MaxValue", out var max)
                    ? max.GetInt32() : 0
            };
        }
        catch { return null; }
    }

    public async Task<bool> StartInstanceAsync(string instanceId, CancellationToken ct = default)
    {
        try
        {
            await CallApiAsync("/Core/Start", instanceId: instanceId, ct: ct);
            return true;
        }
        catch { return false; }
    }

    public async Task<bool> StopInstanceAsync(string instanceId, CancellationToken ct = default)
    {
        try
        {
            await CallApiAsync("/Core/Stop", instanceId: instanceId, ct: ct);
            return true;
        }
        catch { return false; }
    }

    public async Task<bool> RestartInstanceAsync(string instanceId, CancellationToken ct = default)
    {
        try
        {
            await CallApiAsync("/Core/Restart", instanceId: instanceId, ct: ct);
            return true;
        }
        catch { return false; }
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            await EnsureLoggedInAsync();
            return _sessionId is not null;
        }
        catch { return false; }
    }

    private static AmpInstance? ParseInstance(JsonElement inst)
    {
        var id = inst.TryGetProperty("InstanceID", out var iid) ? iid.GetString() : null;
        if (id is null) return null;

        var name = inst.TryGetProperty("InstanceName", out var n) ? n.GetString() ?? "" : "";
        var friendlyName = inst.TryGetProperty("FriendlyName", out var fn) ? fn.GetString() : null;
        var module = inst.TryGetProperty("Module", out var m) ? m.GetString() ?? "" : "";
        var running = inst.TryGetProperty("Running", out var r) && r.GetBoolean();
        var port = inst.TryGetProperty("ApplicationEndpoints", out var eps)
            && eps.ValueKind == JsonValueKind.Array
            && eps.GetArrayLength() > 0
            ? eps[0].TryGetProperty("Endpoint", out var ep) ? ep.GetString() : null
            : null;

        return new AmpInstance
        {
            InstanceId = id,
            InstanceName = name,
            FriendlyName = friendlyName ?? name,
            Module = module,
            IsRunning = running,
            Endpoint = port
        };
    }
}

// --- DTOs ---

public class AmpLoginResponse
{
    public bool success { get; set; }
    public string? sessionID { get; set; }
}

public class AmpInstance
{
    public string InstanceId { get; set; } = "";
    public string InstanceName { get; set; } = "";
    public string FriendlyName { get; set; } = "";
    public string Module { get; set; } = "";
    public bool IsRunning { get; set; }
    public string? Endpoint { get; set; }

    public string GameType => Module switch
    {
        "Minecraft" => "Minecraft",
        "srcds" => "Source Engine",
        "GenericModule" => "Generic",
        "ADS" => "Manager",
        _ => Module
    };

    public string GameIcon => Module switch
    {
        "Minecraft" => "terrain",
        "ADS" => "dns",
        _ => "sports_esports"
    };
}

public class AmpInstanceStatus
{
    public int State { get; set; }
    public string? Uptime { get; set; }
    public double? Cpu { get; set; }
    public double? Memory { get; set; }
    public int ActiveUsers { get; set; }
    public int MaxUsers { get; set; }

    public string StateName => State switch
    {
        0 => "Stopped",
        5 => "Starting",
        10 => "Ready",
        20 => "Starting",
        30 => "Stopping",
        40 => "Installing",
        45 => "Updating",
        _ => "Unknown"
    };

    public bool IsOnline => State == 10;
}
