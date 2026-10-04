using BBF.Api.Services;
using BBF.Shared.Contracts.Users;

namespace BBF.Api.Endpoints;

public static class UsersEndpoints
{
    public static void MapUsersEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/users").RequireAuthorization("AdminOnly");

        group.MapGet("/", async (UserManagementService users, CancellationToken ct) =>
            Results.Ok(await users.GetAllUsersAsync(ct)));

        group.MapPost("/", async (CreateUserRequest request, UserManagementService users, CancellationToken ct) =>
        {
            await users.CreateUserAsync(request, ct);
            return Results.NoContent();
        });

        group.MapPut("/{userId}", async (string userId, UpdateUserRequest request, UserManagementService users, CancellationToken ct) =>
        {
            await users.UpdateUserAsync(userId, request, ct);
            return Results.NoContent();
        });

        group.MapDelete("/{userId}", async (string userId, UserManagementService users, CancellationToken ct) =>
        {
            await users.DeleteUserAsync(userId, ct);
            return Results.NoContent();
        });

        group.MapGet("/roles", async (UserManagementService users, CancellationToken ct) =>
            Results.Ok(await users.GetAllRolesAsync(ct)));

        group.MapPost("/roles/{roleName}", async (string roleName, UserManagementService users, CancellationToken ct) =>
        {
            await users.CreateRoleAsync(roleName, ct);
            return Results.NoContent();
        });

        group.MapDelete("/roles/{roleName}", async (string roleName, UserManagementService users, CancellationToken ct) =>
        {
            await users.DeleteRoleAsync(roleName, ct);
            return Results.NoContent();
        });
    }
}
