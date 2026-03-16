using BBF.Data;
using BBF.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BBF.Services;

public class WikiSearchService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public WikiSearchService(IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<RagSearchResult> SearchAllAsync(string query, int maxResults = 5, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new RagSearchResult();

        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 2)
            .Take(10)
            .ToList();

        if (terms.Count == 0)
            return new RagSearchResult();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Score wiki articles
        var articles = await db.WikiArticles.ToListAsync(ct);
        var scoredArticles = articles
            .Select(a => new
            {
                Article = a,
                Score = terms.Sum(t =>
                    (a.Title.Contains(t, StringComparison.OrdinalIgnoreCase) ? 3 : 0) +
                    (a.Tags?.Contains(t, StringComparison.OrdinalIgnoreCase) == true ? 2 : 0) +
                    (a.Content.Contains(t, StringComparison.OrdinalIgnoreCase) ? 1 : 0))
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score);

        // Score documents (only those with extracted text)
        var documents = await db.Documents
            .Where(d => d.ExtractedText != null)
            .ToListAsync(ct);

        var scoredDocuments = documents
            .Select(d => new
            {
                Document = d,
                Score = terms.Sum(t =>
                    (d.FileName.Contains(t, StringComparison.OrdinalIgnoreCase) ? 3 : 0) +
                    (d.Category?.Contains(t, StringComparison.OrdinalIgnoreCase) == true ? 2 : 0) +
                    (d.Description?.Contains(t, StringComparison.OrdinalIgnoreCase) == true ? 2 : 0) +
                    (d.ExtractedText!.Contains(t, StringComparison.OrdinalIgnoreCase) ? 1 : 0))
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score);

        // Merge and take top results by score
        var merged = scoredArticles
            .Select(x => new ScoredItem(x.Score, x.Article.Title, FormatArticleContent(x.Article), "wiki"))
            .Concat(scoredDocuments
                .Select(x => new ScoredItem(x.Score, x.Document.FileName, FormatDocumentContent(x.Document), "document")))
            .OrderByDescending(x => x.Score)
            .Take(maxResults)
            .ToList();

        return new RagSearchResult
        {
            Items = merged,
            ArticleCount = merged.Count(i => i.Source == "wiki"),
            DocumentCount = merged.Count(i => i.Source == "document")
        };
    }

    public static string FormatAsContext(RagSearchResult result)
    {
        if (result.Items.Count == 0) return string.Empty;

        var lines = new List<string>
        {
            "Here is relevant documentation from the local knowledge base:"
        };

        foreach (var item in result.Items)
        {
            lines.Add($"\n--- {item.Title} ({item.Source}) ---");
            var content = item.Content.Length > 3000
                ? item.Content[..3000] + "\n...(truncated)"
                : item.Content;
            lines.Add(content);
        }

        lines.Add("\nUse the above documentation to answer accurately. If the documentation doesn't cover the question, say so and answer from your general knowledge.");

        return string.Join("\n", lines);
    }

    private static string FormatArticleContent(WikiArticle article)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(article.Category))
            parts.Add($"Category: {article.Category}");
        if (!string.IsNullOrWhiteSpace(article.Tags))
            parts.Add($"Tags: {article.Tags}");
        parts.Add(article.Content);
        return string.Join("\n", parts);
    }

    private static string FormatDocumentContent(Document doc)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(doc.Category))
            parts.Add($"Category: {doc.Category}");
        if (!string.IsNullOrWhiteSpace(doc.Description))
            parts.Add($"Description: {doc.Description}");
        parts.Add(doc.ExtractedText!);
        return string.Join("\n", parts);
    }
}

public record ScoredItem(int Score, string Title, string Content, string Source);

public class RagSearchResult
{
    public List<ScoredItem> Items { get; set; } = [];
    public int ArticleCount { get; set; }
    public int DocumentCount { get; set; }
}
