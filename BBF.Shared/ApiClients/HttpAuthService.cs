using System.Net.Http.Json;
using BBF.Shared.Contracts.Auth;

namespace BBF.Shared.ApiClients;

/// <summary>
/// Calls BBF.Api's password-login endpoint directly. Used by BBF.Mobile before it has a token at all —
/// registered on a plain HttpClient with no ApiAuthDelegatingHandler attached, unlike every other
/// Http*Service here. BBF.Web never uses this; it has its own cookie-session-to-JWT exchange instead.
/// </summary>
public class HttpAuthService(HttpClient http) : IAuthService
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/api/auth/login", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
        return await response.Content.ReadFromJsonAsync<LoginResponse>(ct)
            ?? throw new InvalidOperationException("Empty response from login.");
    }
}
