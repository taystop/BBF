using System.Text.Json;
using BBF.Api.BackgroundServices;
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

        // Called by Plaid, not by a user — anonymous, so every request must pass signature verification.
        // Always answers quickly; the sync itself runs on PlaidSyncBackgroundService.
        app.MapPost("/api/plaid/webhook", async (HttpRequest request, PlaidService plaid, PlaidSyncQueue queue,
            ILogger<PlaidService> logger, CancellationToken ct) =>
        {
            using var buffer = new MemoryStream();
            await request.Body.CopyToAsync(buffer, ct);
            var body = buffer.ToArray();

            if (!await plaid.VerifyWebhookAsync(request.Headers["Plaid-Verification"].ToString(), body, ct))
            {
                logger.LogWarning("Rejected Plaid webhook with an invalid signature");
                return Results.Unauthorized();
            }

            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            var type = root.TryGetProperty("webhook_type", out var t) ? t.GetString() : null;
            var code = root.TryGetProperty("webhook_code", out var c) ? c.GetString() : null;
            var itemId = root.TryGetProperty("item_id", out var i) ? i.GetString() : null;

            if (type == "TRANSACTIONS" && code == "SYNC_UPDATES_AVAILABLE" && itemId is not null)
            {
                if (await plaid.FindConnectionIdByItemIdAsync(itemId, ct) is int connectionId)
                    queue.Enqueue(connectionId);
            }
            else
            {
                logger.LogInformation("Plaid webhook {Type}/{Code} for item {ItemId} (no action)", type, code, itemId);
            }

            return Results.Ok();
        }).AllowAnonymous();

        var group = app.MapGroup("/api/groups/{groupId:int}/plaid").RequireAuthorization();

        group.MapPost("/exchange-token", async (int groupId, ExchangePublicTokenRequest request, PlaidService plaid, CancellationToken ct) =>
            Results.Ok(await plaid.ExchangePublicTokenAsync(groupId, request, ct)));

        group.MapGet("/connections", async (int groupId, PlaidService plaid, CancellationToken ct) =>
            Results.Ok(await plaid.GetConnectionsAsync(groupId, ct)));

        group.MapPost("/connections/{connectionId:int}/sync", async (int groupId, int connectionId, PlaidService plaid, CancellationToken ct) =>
            Results.Ok(await plaid.SyncTransactionsAsync(groupId, connectionId, ct)));

        group.MapPost("/connections/{connectionId:int}/update-link-token", async (int groupId, int connectionId, PlaidService plaid, CancellationToken ct) =>
            Results.Ok(new { linkToken = await plaid.CreateUpdateLinkTokenAsync(groupId, connectionId, ct) }));

        group.MapPost("/connections/{connectionId:int}/refresh-accounts", async (int groupId, int connectionId, PlaidService plaid, CancellationToken ct) =>
        {
            await plaid.RefreshAccountsAsync(groupId, connectionId, ct);
            return Results.NoContent();
        });

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
