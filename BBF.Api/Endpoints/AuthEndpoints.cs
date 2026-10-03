using BBF.Api.Auth;
using BBF.Data;
using BBF.Shared.Contracts.Auth;
using Microsoft.AspNetCore.Identity;

namespace BBF.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/login", async (
            LoginRequest request,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            JwtTokenService tokenService) =>
        {
            var user = await userManager.FindByNameAsync(request.UserName);
            if (user is null)
                return Results.Unauthorized();

            var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (!result.Succeeded)
                return Results.Unauthorized();

            var issued = tokenService.IssueToken(user);
            return Results.Ok(new LoginResponse(issued.AccessToken, issued.ExpiresAtUtc, user.Id, user.UserName ?? user.Id));
        });

        // Lets BBF.Web exchange its already-authenticated cookie session for a JWT, so it can call the
        // rest of BBF.Api the same way BBF.Mobile does. Trusted only because Web and Api sit on the same
        // LAN and share this key — never exposed to the public internet tunnel ingress (see Phase 5).
        group.MapPost("/internal-token", async (
            InternalTokenRequest request,
            HttpRequest httpRequest,
            IConfiguration configuration,
            UserManager<ApplicationUser> userManager,
            JwtTokenService tokenService) =>
        {
            var expectedKey = configuration["Jwt:InternalServiceKey"];
            var providedKey = httpRequest.Headers["X-Internal-Key"].ToString();
            if (string.IsNullOrEmpty(expectedKey) || providedKey != expectedKey)
                return Results.Unauthorized();

            var user = await userManager.FindByIdAsync(request.UserId);
            if (user is null)
                return Results.NotFound();

            var issued = tokenService.IssueToken(user);
            return Results.Ok(new LoginResponse(issued.AccessToken, issued.ExpiresAtUtc, user.Id, user.UserName ?? user.Id));
        });
    }
}
