using BBF.Api.Services;
using BBF.Shared.Contracts.MealPlanning;

namespace BBF.Api.Endpoints;

public static class GroceryListsEndpoints
{
    public static void MapGroceryListsEndpoints(this WebApplication app)
    {
        var weeks = app.MapGroup("/api/groups/{groupId:int}/meal-plan/weeks/{weekId:int}/grocery-list").RequireAuthorization();

        weeks.MapGet("/", async (int groupId, int weekId, GroceryListsService groceryLists, CancellationToken ct) =>
        {
            var list = await groceryLists.GetAsync(groupId, weekId, ct);
            return list is null ? Results.NotFound() : Results.Ok(list);
        });

        weeks.MapPost("/generate", async (int groupId, int weekId, GroceryListsService groceryLists, CancellationToken ct) =>
        {
            var list = await groceryLists.GenerateAsync(groupId, weekId, ct);
            return list is null ? Results.NotFound() : Results.Ok(list);
        });

        var lists = app.MapGroup("/api/groups/{groupId:int}/grocery-lists/{groceryListId:int}").RequireAuthorization();

        lists.MapPost("/items", async (int groupId, int groceryListId, AddManualGroceryItemRequest request, GroceryListsService groceryLists, CancellationToken ct) =>
        {
            await groceryLists.AddManualItemAsync(groupId, groceryListId, request, ct);
            return Results.NoContent();
        });

        var items = app.MapGroup("/api/groups/{groupId:int}/grocery-list-items/{itemId:int}").RequireAuthorization();

        items.MapDelete("/", async (int groupId, int itemId, GroceryListsService groceryLists, CancellationToken ct) =>
        {
            await groceryLists.RemoveItemAsync(groupId, itemId, ct);
            return Results.NoContent();
        });

        items.MapPost("/checked", async (int groupId, int itemId, bool isChecked, GroceryListsService groceryLists, CancellationToken ct) =>
        {
            await groceryLists.SetCheckedAsync(groupId, itemId, isChecked, ct);
            return Results.NoContent();
        });
    }
}
