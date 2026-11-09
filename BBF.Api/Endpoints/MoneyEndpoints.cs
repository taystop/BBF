using BBF.Api.Services;
using BBF.Shared.Contracts.Money;

namespace BBF.Api.Endpoints;

public static class MoneyEndpoints
{
    public static void MapMoneyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/groups/{groupId:int}/money").RequireAuthorization();

        group.MapGet("/summary", async (int groupId, MoneyService money, CancellationToken ct) =>
            Results.Ok(await money.GetSummaryAsync(groupId, ct)));

        group.MapGet("/forecast", async (int groupId, int? days, MoneyService money, CancellationToken ct) =>
            Results.Ok(await money.GetForecastAsync(groupId, days ?? 45, ct)));

        group.MapGet("/review", async (int groupId, MoneyService money, CancellationToken ct) =>
            Results.Ok(await money.GetToReviewAsync(groupId, ct)));

        group.MapPost("/review/{transactionId:int}/categorize", async (int groupId, int transactionId, CategorizeRequest request, MoneyService money, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await money.CategorizeAsync(groupId, transactionId, request, ct));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/merchant-rules", async (int groupId, MoneyService money, CancellationToken ct) =>
            Results.Ok(await money.GetMerchantRulesAsync(groupId, ct)));

        group.MapDelete("/merchant-rules/{ruleId:int}", async (int groupId, int ruleId, MoneyService money, CancellationToken ct) =>
        {
            await money.DeleteMerchantRuleAsync(groupId, ruleId, ct);
            return Results.NoContent();
        });

        group.MapGet("/suggestions", async (int groupId, MoneyService money, CancellationToken ct) =>
            Results.Ok(await money.GetSuggestionsAsync(groupId, ct)));

        group.MapPost("/suggestions/dismiss", async (int groupId, DismissSuggestionRequest request, MoneyService money, CancellationToken ct) =>
        {
            try
            {
                await money.DismissSuggestionAsync(groupId, request, ct);
                return Results.NoContent();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });
    }
}
