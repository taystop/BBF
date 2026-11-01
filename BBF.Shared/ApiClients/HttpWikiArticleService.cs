using System.Net.Http.Json;
using BBF.Shared.Contracts.Wiki;

namespace BBF.Shared.ApiClients;

public class HttpWikiArticleService(HttpClient http) : IWikiArticleService
{
    public async Task<List<WikiArticleDto>> GetAllAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<WikiArticleDto>>("/api/wiki/articles", ct) ?? [];

    public async Task<WikiArticleDto> SaveAsync(UpsertWikiArticleRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/api/wiki/articles", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
        return await response.Content.ReadFromJsonAsync<WikiArticleDto>(ct)
            ?? throw new InvalidOperationException("Empty response saving wiki article.");
    }

    public async Task DeleteAsync(int articleId, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"/api/wiki/articles/{articleId}", ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }
}
