using BBF.Data;
using BBF.Shared.Contracts.Users;
using Microsoft.AspNetCore.Identity;

namespace BBF.Api.Services;

/// <summary>
/// Backs the Admin-only Users page — gated by the "AdminOnly" authorization policy at the endpoint
/// level, matching the original page's [Authorize(Roles = "Admin")]. IdentityResult failures (bad
/// password policy, duplicate email, etc.) are surfaced as InvalidOperationExceptions so the API's
/// exception-handler middleware turns them into a 409 with the real error text in the body.
/// </summary>
public class UserManagementService(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
{
    public async Task<List<UserSummaryDto>> GetAllUsersAsync(CancellationToken ct = default)
    {
        var users = userManager.Users.OrderBy(u => u.Email).ToList();
        var result = new List<UserSummaryDto>(users.Count);
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            var isLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;
            result.Add(new UserSummaryDto(user.Id, user.Email, user.EmailConfirmed, isLockedOut, roles.ToList()));
        }
        return result;
    }

    public Task<List<string>> GetAllRolesAsync(CancellationToken ct = default) =>
        Task.FromResult(roleManager.Roles.Select(r => r.Name!).OrderBy(r => r).ToList());

    public async Task CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var user = new ApplicationUser();
        await userManager.SetUserNameAsync(user, request.Email);
        await userManager.SetEmailAsync(user, request.Email);

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
            throw new InvalidOperationException(string.Join(", ", createResult.Errors.Select(e => e.Description)));

        // Confirm email automatically for admin-created users — matches the original page's behavior.
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        await userManager.ConfirmEmailAsync(user, token);

        if (request.Roles.Count > 0)
            await userManager.AddToRolesAsync(user, request.Roles);
    }

    public async Task UpdateUserAsync(string userId, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException($"No user found with id '{userId}'.");

        if (user.Email != request.Email)
        {
            await userManager.SetUserNameAsync(user, request.Email);
            await userManager.SetEmailAsync(user, request.Email);
        }

        if (!string.IsNullOrEmpty(request.NewPassword))
        {
            var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
            var pwResult = await userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);
            if (!pwResult.Succeeded)
                throw new InvalidOperationException(string.Join(", ", pwResult.Errors.Select(e => e.Description)));
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        var toRemove = currentRoles.Except(request.Roles).ToList();
        var toAdd = request.Roles.Except(currentRoles).ToList();

        if (toRemove.Count > 0) await userManager.RemoveFromRolesAsync(user, toRemove);
        if (toAdd.Count > 0) await userManager.AddToRolesAsync(user, toAdd);
    }

    public async Task DeleteUserAsync(string userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return;
        await userManager.DeleteAsync(user);
    }

    public async Task CreateRoleAsync(string roleName, CancellationToken ct = default)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
            await roleManager.CreateAsync(new IdentityRole(roleName));
    }

    public async Task DeleteRoleAsync(string roleName, CancellationToken ct = default)
    {
        var role = await roleManager.FindByNameAsync(roleName);
        if (role is null) return;
        await roleManager.DeleteAsync(role);
    }
}
