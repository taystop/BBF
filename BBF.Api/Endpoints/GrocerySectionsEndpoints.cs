using BBF.Api.Services;
using BBF.Shared.Contracts.MealPlanning;

namespace BBF.Api.Endpoints;

public static class GrocerySectionsEndpoints
{
    public static void MapGrocerySectionsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/groups/{groupId:int}/grocery-sections").RequireAuthorization();

        group.MapGet("/", async (int groupId, GrocerySectionsService sections, CancellationToken ct) =>
            Results.Ok(await sections.GetSectionsAsync(groupId, ct)));

        group.MapPost("/", async (int groupId, CreateGrocerySectionRequest request, GrocerySectionsService sections, CancellationToken ct) =>
            Results.Ok(await sections.CreateAsync(groupId, request, ct)));

        group.MapPut("/{sectionId:int}", async (int groupId, int sectionId, UpdateGrocerySectionRequest request, GrocerySectionsService sections, CancellationToken ct) =>
        {
            await sections.UpdateAsync(groupId, sectionId, request, ct);
            return Results.NoContent();
        });

        group.MapDelete("/{sectionId:int}", async (int groupId, int sectionId, GrocerySectionsService sections, CancellationToken ct) =>
        {
            await sections.DeleteAsync(groupId, sectionId, ct);
            return Results.NoContent();
        });

        group.MapPost("/reorder", async (int groupId, ReorderGrocerySectionsRequest request, GrocerySectionsService sections, CancellationToken ct) =>
        {
            await sections.ReorderAsync(groupId, request, ct);
            return Results.NoContent();
        });
    }
}
