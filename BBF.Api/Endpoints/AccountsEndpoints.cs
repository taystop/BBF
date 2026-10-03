using BBF.Api.Services;
using BBF.Shared.Contracts.Accounts;

namespace BBF.Api.Endpoints;

public static class AccountsEndpoints
{
    public static void MapAccountsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/groups/{groupId:int}/accounts").RequireAuthorization();

        group.MapGet("/", async (int groupId, AccountsService accounts, CancellationToken ct) =>
            Results.Ok(await accounts.GetAccountsAsync(groupId, ct)));

        group.MapPost("/", async (int groupId, CreateAccountRequest request, AccountsService accounts, CancellationToken ct) =>
            Results.Ok(await accounts.CreateAsync(groupId, request, ct)));

        group.MapPut("/{accountId:int}", async (int groupId, int accountId, UpdateAccountRequest request, AccountsService accounts, CancellationToken ct) =>
        {
            await accounts.UpdateAsync(groupId, accountId, request, ct);
            return Results.NoContent();
        });

        group.MapPost("/{accountId:int}/active", async (int groupId, int accountId, bool isActive, AccountsService accounts, CancellationToken ct) =>
        {
            await accounts.SetActiveAsync(groupId, accountId, isActive, ct);
            return Results.NoContent();
        });
    }
}
