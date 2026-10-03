using BBF.Api.Services;
using BBF.Shared.Contracts.Plaid;

namespace BBF.Api.Endpoints;

public static class PlaidEndpoints
{
    public static void MapPlaidEndpoints(this WebApplication app)
    {
        app.MapPost("/api/plaid/create-link-token", async (PlaidService plaid, CancellationToken ct) =>
            Results.Ok(new { linkToken = await plaid.CreateLinkTokenAsync(ct) })
        ).RequireAuthorization();

        var group = app.MapGroup("/api/groups/{groupId:int}/plaid").RequireAuthorization();

        group.MapPost("/exchange-token", async (int groupId, ExchangePublicTokenRequest request, PlaidService plaid, CancellationToken ct) =>
            Results.Ok(await plaid.ExchangePublicTokenAsync(groupId, request, ct)));

        group.MapGet("/connections", async (int groupId, PlaidService plaid, CancellationToken ct) =>
            Results.Ok(await plaid.GetConnectionsAsync(groupId, ct)));

        group.MapPost("/connections/{connectionId:int}/sync", async (int groupId, int connectionId, PlaidService plaid, CancellationToken ct) =>
            Results.Ok(await plaid.SyncTransactionsAsync(groupId, connectionId, ct)));

        group.MapDelete("/connections/{connectionId:int}", async (int groupId, int connectionId, PlaidService plaid, CancellationToken ct) =>
        {
            await plaid.RemoveConnectionAsync(groupId, connectionId, ct);
            return Results.NoContent();
        });

        group.MapPut("/accounts/{accountId:int}/name", async (int groupId, int accountId, UpdatePlaidAccountNameRequest request, PlaidService plaid, CancellationToken ct) =>
        {
            await plaid.UpdateAccountNameAsync(groupId, accountId, request, ct);
            return Results.NoContent();
        });
    }
}
