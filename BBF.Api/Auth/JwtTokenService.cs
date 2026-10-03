using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BBF.Data;
using Microsoft.IdentityModel.Tokens;

namespace BBF.Api.Auth;

public record IssuedToken(string AccessToken, DateTime ExpiresAtUtc);

/// <summary>
/// Issues the JWTs both BBF.Mobile (after password login) and BBF.Web (after exchanging its cookie
/// session via the internal-token endpoint) use as a Bearer token against the rest of BBF.Api.
/// v1 simplification: a single longer-lived access token, no refresh-token rotation — acceptable for
/// a personal home app; revisit if this ever needs tighter token lifetimes.
/// </summary>
public class JwtTokenService(IConfiguration configuration)
{
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromDays(14);

    public IssuedToken IssueToken(ApplicationUser user, IEnumerable<string>? roles = null)
    {
        var signingKey = configuration["Jwt:SigningKey"];
        if (string.IsNullOrEmpty(signingKey))
            throw new InvalidOperationException("Jwt:SigningKey is not configured.");

        var expiresAtUtc = DateTime.UtcNow.Add(AccessTokenLifetime);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? user.Id),
        };
        claims.AddRange((roles ?? []).Select(r => new Claim(ClaimTypes.Role, r)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new IssuedToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
