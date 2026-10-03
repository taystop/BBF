using BBF.Api.Services;
using BBF.Shared.Contracts.Budget;

namespace BBF.Api.Endpoints;

public static class BudgetCategoriesEndpoints
{
    public static void MapBudgetCategoriesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/groups/{groupId:int}/budget/categories").RequireAuthorization();

        group.MapGet("/", async (int groupId, BudgetCategoriesService categories, CancellationToken ct) =>
            Results.Ok(await categories.GetCategoriesAsync(groupId, ct)));

        group.MapPost("/", async (int groupId, UpsertBudgetCategoryRequest request, BudgetCategoriesService categories, CancellationToken ct) =>
            Results.Ok(await categories.CreateAsync(groupId, request, ct)));

        group.MapPut("/{categoryId:int}", async (int groupId, int categoryId, UpsertBudgetCategoryRequest request, BudgetCategoriesService categories, CancellationToken ct) =>
        {
            await categories.UpdateAsync(groupId, categoryId, request, ct);
            return Results.NoContent();
        });

        group.MapDelete("/{categoryId:int}", async (int groupId, int categoryId, BudgetCategoriesService categories, CancellationToken ct) =>
            Results.Ok(await categories.DeleteAsync(groupId, categoryId, ct)));
    }
}
