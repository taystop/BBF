using BBF.Api.Services;
using BBF.Shared.Contracts.Budget;

namespace BBF.Api.Endpoints;

public static class BudgetEndpoints
{
    public static void MapBudgetEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/groups/{groupId:int}/budget/periods").RequireAuthorization();

        group.MapGet("/", async (int groupId, DateTime? month, BudgetPeriodsService periods, CancellationToken ct) =>
        {
            if (month is null)
                return Results.Ok(await periods.GetOrCreateCurrentPeriodAsync(groupId, ct));

            var period = await periods.GetPeriodAsync(groupId, month.Value, ct);
            return period is null ? Results.NotFound() : Results.Ok(period);
        });

        group.MapPut("/{periodId:int}/line-items", async (int groupId, int periodId, UpsertLineItemRequest request, BudgetPeriodsService periods, CancellationToken ct) =>
        {
            await periods.UpsertLineItemAsync(groupId, periodId, request, ct);
            return Results.NoContent();
        });

        group.MapPost("/{periodId:int}/close", async (int groupId, int periodId, BudgetPeriodsService periods, CancellationToken ct) =>
        {
            await periods.ClosePeriodAsync(groupId, periodId, ct);
            return Results.NoContent();
        });
    }
}
