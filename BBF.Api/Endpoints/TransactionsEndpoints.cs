using BBF.Api.Services;
using BBF.Shared.Contracts.Transactions;
using Microsoft.AspNetCore.Mvc;

namespace BBF.Api.Endpoints;

public static class TransactionsEndpoints
{
    public static void MapTransactionsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/groups/{groupId:int}/transactions").RequireAuthorization();

        group.MapGet("/", async (int groupId, TransactionsService transactions, CancellationToken ct) =>
            Results.Ok(await transactions.GetTransactionsAsync(groupId, ct)));

        group.MapPost("/", async (int groupId, CreateTransactionRequest request, TransactionsService transactions, CancellationToken ct) =>
            Results.Ok(await transactions.CreateAsync(groupId, request, ct)));

        group.MapPut("/{transactionId:int}", async (int groupId, int transactionId, UpdateTransactionRequest request, TransactionsService transactions, CancellationToken ct) =>
        {
            await transactions.UpdateAsync(groupId, transactionId, request, ct);
            return Results.NoContent();
        });

        group.MapDelete("/{transactionId:int}", async (int groupId, int transactionId, TransactionsService transactions, CancellationToken ct) =>
        {
            await transactions.DeleteAsync(groupId, transactionId, ct);
            return Results.NoContent();
        });

        group.MapPost("/import-csv", async (int groupId, [FromForm] IFormFile file, [FromForm] int? accountId, TransactionsService transactions, CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            var result = await transactions.ImportCsvAsync(groupId, accountId, stream, ct);
            return Results.Ok(result);
        }).DisableAntiforgery();
    }
}
