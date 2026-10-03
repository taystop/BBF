using System.Net.Http.Json;

namespace BBF.ApiClients;

public static class HttpResponseMessageExtensions
{
    /// <summary>
    /// Like EnsureSuccessStatusCode, but throws InvalidOperationException carrying BBF.Api's
    /// {"error": "..."} body text when available, instead of a generic HttpRequestException whose
    /// message is just the status code. Lets UI code show the server's actual validation message
    /// (e.g. "User is already a member of this group.") instead of a generic failure.
    /// </summary>
    public static async Task EnsureSuccessWithErrorBodyAsync(this HttpResponseMessage response, CancellationToken ct = default)
    {
        if (response.IsSuccessStatusCode) return;

        string? serverMessage = null;
        try
        {
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(ct);
            serverMessage = body?.GetValueOrDefault("error");
        }
        catch
        {
            // Body wasn't the expected {"error": "..."} shape — fall back to the generic message below.
        }

        throw new InvalidOperationException(serverMessage ?? $"Request failed with status {(int)response.StatusCode}.");
    }
}
