using System.Security.Claims;
using System.Text.Json;
using BBF.Shared.ApiClients;
using BBF.Shared.Contracts.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Maui.Storage;

namespace BBF.Mobile.Auth;

/// <summary>
/// Mobile's equivalent of BBF.Web's cookie session: the JWT from a direct password login, persisted in
/// SecureStorage so it survives app restarts, and read back into a ClaimsPrincipal on first use. Also
/// the IApiAuthTokenProvider every BBF.Api-backed typed client attaches to for its Bearer token — one
/// instance plays both roles since, unlike BBF.Web, there's no per-circuit scope to bridge.
///
/// Decodes the JWT payload directly instead of pulling in System.IdentityModel.Tokens.Jwt — this never
/// validates the signature (BBF.Api does that on every call), it only reads claims already inside a
/// token this device itself received from a successful login. JwtSecurityTokenHandler's default outbound
/// claim map means the wire format uses short claim names ("unique_name", "role"), not the full
/// ClaimTypes.* URIs BBF.Api issued them with — the identity below is built using those short names.
/// </summary>
public class MobileAuthenticationStateProvider : AuthenticationStateProvider, IApiAuthTokenProvider
{
    private const string TokenKey = "bbf_access_token";
    private const string ExpiresKey = "bbf_token_expires_at_utc";
    private const string NameClaimType = "unique_name";
    private const string RoleClaimType = "role";

    private static readonly ClaimsPrincipal AnonymousUser = new(new ClaimsIdentity());

    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _loaded;
    private string? _token;
    private DateTime _expiresAtUtc;
    private ClaimsPrincipal _currentUser = AnonymousUser;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        await EnsureLoadedAsync();
        return new AuthenticationState(_currentUser);
    }

    public async Task<string> GetTokenAsync(CancellationToken ct = default)
    {
        await EnsureLoadedAsync();
        if (_token is null)
            throw new InvalidOperationException("Not signed in.");
        return _token;
    }

    public async Task SignInAsync(LoginResponse login)
    {
        await SecureStorage.Default.SetAsync(TokenKey, login.AccessToken);
        await SecureStorage.Default.SetAsync(ExpiresKey, login.ExpiresAtUtc.ToString("o"));

        _token = login.AccessToken;
        _expiresAtUtc = login.ExpiresAtUtc;
        _currentUser = BuildPrincipal(login.AccessToken);
        _loaded = true;

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }

    public void SignOut()
    {
        SecureStorage.Default.Remove(TokenKey);
        SecureStorage.Default.Remove(ExpiresKey);

        _token = null;
        _currentUser = AnonymousUser;
        _loaded = true;

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }

    private async Task EnsureLoadedAsync()
    {
        if (_loaded) return;

        await _lock.WaitAsync();
        try
        {
            if (_loaded) return;
            _loaded = true;

            var token = await SecureStorage.Default.GetAsync(TokenKey);
            var expiresRaw = await SecureStorage.Default.GetAsync(ExpiresKey);
            if (token is null || expiresRaw is null) return;

            if (!DateTime.TryParse(expiresRaw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var expiresAtUtc)
                || DateTime.UtcNow >= expiresAtUtc)
            {
                SecureStorage.Default.Remove(TokenKey);
                SecureStorage.Default.Remove(ExpiresKey);
                return;
            }

            _token = token;
            _expiresAtUtc = expiresAtUtc;
            _currentUser = BuildPrincipal(token);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static ClaimsPrincipal BuildPrincipal(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2) return AnonymousUser;

        using var payload = JsonDocument.Parse(Base64UrlDecode(parts[1]));

        var claims = new List<Claim>();
        foreach (var property in payload.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in property.Value.EnumerateArray())
                    claims.Add(new Claim(property.Name, item.ToString()));
            }
            else
            {
                claims.Add(new Claim(property.Name, property.Value.ToString()));
            }
        }

        var identity = new ClaimsIdentity(claims, authenticationType: "jwt", nameType: NameClaimType, roleType: RoleClaimType);
        return new ClaimsPrincipal(identity);
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }
}
