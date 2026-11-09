using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Security.Claims;
using BBF.Shared.ApiClients;
using BBF.Shared.Contracts.Auth;

namespace BBF.ApiClients;

/// <summary>
/// BBF.Web keeps its own cookie-based Identity login untouched — this is the bridge that lets it call
/// BBF.Api the same way BBF.Mobile does. On first use per circuit, it exchanges the already-authenticated
/// user's id for a short-lived JWT via BBF.Api's internal-token endpoint (trusted via a shared key,
/// because Web and Api sit on the same LAN), then caches that token for the rest of the circuit's life.
/// Scoped — one instance (and one cached token) per circuit, matching how UserContextService worked.
/// </summary>
public class ApiAuthTokenProvider(
    IHttpClientFactory httpClientFactory,
    IConfiguration config) : IApiAuthTokenProvider
{
    // Shared by every circuit, so opening tabs or reloading pages reuses a recent token instead of
    // asking BBF.Api for a new one each time. Kept short so role changes still show up within the hour.
    private static readonly TimeSpan SharedReuse = TimeSpan.FromHours(1);
    private static readonly ConcurrentDictionary<string, (string Token, DateTime ExpiresAtUtc, DateTime IssuedAtUtc)> SharedTokens = new();

    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _cachedToken;
    private DateTime _expiresAtUtc;

    public async Task<string> GetTokenAsync(CancellationToken ct = default)
    {
        if (IsStillValid()) return _cachedToken!;

        await _lock.WaitAsync(ct);
        try
        {
            if (IsStillValid()) return _cachedToken!;

            var authState = await CircuitAuthStateAccessor.StateTask;
            var userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? throw new InvalidOperationException("No authenticated user on this circuit.");

            if (SharedTokens.TryGetValue(userId, out var shared)
                && DateTime.UtcNow - shared.IssuedAtUtc < SharedReuse
                && DateTime.UtcNow < shared.ExpiresAtUtc.AddMinutes(-5))
            {
                _cachedToken = shared.Token;
                _expiresAtUtc = shared.ExpiresAtUtc;
                return _cachedToken;
            }

            var internalKey = config["Jwt:InternalServiceKey"];
            if (string.IsNullOrEmpty(internalKey))
                throw new InvalidOperationException("Jwt:InternalServiceKey is not configured.");

            using var client = httpClientFactory.CreateClient("BBF.Api.Internal");
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/internal-token")
            {
                Content = JsonContent.Create(new InternalTokenRequest(userId))
            };
            request.Headers.Add("X-Internal-Key", internalKey);

            using var response = await client.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var login = await response.Content.ReadFromJsonAsync<LoginResponse>(ct)
                ?? throw new InvalidOperationException("Empty response from BBF.Api's internal-token endpoint.");

            _cachedToken = login.AccessToken;
            _expiresAtUtc = login.ExpiresAtUtc;
            SharedTokens[userId] = (login.AccessToken, login.ExpiresAtUtc, DateTime.UtcNow);
            return _cachedToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    private bool IsStillValid() =>
        _cachedToken is not null && DateTime.UtcNow < _expiresAtUtc.AddMinutes(-5);
}
