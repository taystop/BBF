using BBF.Data;
using BBF.Data.Entities;
using BBF.Shared.Contracts.Wiki;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Services;

/// <summary>WikiArticle is global, not group-scoped — unchanged from the original.</summary>
public class WikiArticlesService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    public async Task<List<WikiArticleDto>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.WikiArticles
            .OrderByDescending(a => a.UpdatedAt)
            .Select(a => new WikiArticleDto(a.Id, a.Title, a.Content, a.Category, a.Tags, a.CreatedAt, a.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<WikiArticleDto> SaveAsync(UpsertWikiArticleRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        WikiArticle article;
        if (request.Id == 0)
        {
            article = new WikiArticle { CreatedAt = DateTime.UtcNow };
            db.WikiArticles.Add(article);
        }
        else
        {
            article = await db.WikiArticles.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
                ?? throw new KeyNotFoundException($"Wiki article {request.Id} not found.");
        }

        article.Title = request.Title;
        article.Content = request.Content;
        article.Category = request.Category;
        article.Tags = request.Tags;
        article.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return new WikiArticleDto(article.Id, article.Title, article.Content, article.Category, article.Tags, article.CreatedAt, article.UpdatedAt);
    }

    public async Task DeleteAsync(int articleId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var entity = await db.WikiArticles.FirstOrDefaultAsync(a => a.Id == articleId, ct);
        if (entity is null) return;

        db.WikiArticles.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}
