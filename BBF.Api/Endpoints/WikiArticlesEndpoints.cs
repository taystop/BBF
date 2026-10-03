using BBF.Api.Services;
using BBF.Shared.Contracts.Wiki;

namespace BBF.Api.Endpoints;

public static class WikiArticlesEndpoints
{
    public static void MapWikiArticlesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/wiki/articles").RequireAuthorization();

        group.MapGet("/", async (WikiArticlesService wiki, CancellationToken ct) =>
            Results.Ok(await wiki.GetAllAsync(ct)));

        group.MapPost("/", async (UpsertWikiArticleRequest request, WikiArticlesService wiki, CancellationToken ct) =>
            Results.Ok(await wiki.SaveAsync(request, ct)));

        group.MapDelete("/{articleId:int}", async (int articleId, WikiArticlesService wiki, CancellationToken ct) =>
        {
            await wiki.DeleteAsync(articleId, ct);
            return Results.NoContent();
        });
    }
}
