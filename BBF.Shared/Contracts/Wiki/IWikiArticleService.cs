namespace BBF.Shared.Contracts.Wiki;

/// <summary>WikiArticle is global, not group-scoped (matches the original — a shared household knowledge base).</summary>
public interface IWikiArticleService
{
    Task<List<WikiArticleDto>> GetAllAsync(CancellationToken ct = default);
    Task<WikiArticleDto> SaveAsync(UpsertWikiArticleRequest request, CancellationToken ct = default);
    Task DeleteAsync(int articleId, CancellationToken ct = default);
}
