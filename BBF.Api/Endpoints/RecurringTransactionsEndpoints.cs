using BBF.Api.Services;
using BBF.Shared.Contracts.Recurring;

namespace BBF.Api.Endpoints;

public static class RecurringTransactionsEndpoints
{
    public static void MapRecurringTransactionsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/groups/{groupId:int}/recurring-transactions").RequireAuthorization();

        group.MapGet("/", async (int groupId, RecurringTransactionsService recurring, CancellationToken ct) =>
            Results.Ok(await recurring.GetActiveAsync(groupId, ct)));

        group.MapPost("/", async (int groupId, CreateRecurringTransactionRequest request, RecurringTransactionsService recurring, CancellationToken ct) =>
            Results.Ok(await recurring.CreateAsync(groupId, request, ct)));

        group.MapPut("/{ruleId:int}", async (int groupId, int ruleId, UpdateRecurringTransactionRequest request, RecurringTransactionsService recurring, CancellationToken ct) =>
        {
            await recurring.UpdateAsync(groupId, ruleId, request, ct);
            return Results.NoContent();
        });

        group.MapDelete("/{ruleId:int}", async (int groupId, int ruleId, RecurringTransactionsService recurring, CancellationToken ct) =>
        {
            await recurring.DeleteAsync(groupId, ruleId, ct);
            return Results.NoContent();
        });
    }
}
