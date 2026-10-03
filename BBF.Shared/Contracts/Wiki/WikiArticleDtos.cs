namespace BBF.Shared.Contracts.Wiki;

public record WikiArticleDto(int Id, string Title, string Content, string? Category, string? Tags, DateTime CreatedAt, DateTime UpdatedAt);

/// <summary>Id 0 means create; any other value updates that article.</summary>
public record UpsertWikiArticleRequest(int Id, string Title, string Content, string? Category, string? Tags);
