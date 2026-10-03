namespace BBF.Shared.Contracts.Chat;

public record ChatMessageDto(int Id, string Role, string Content, DateTime CreatedAt);

public record ChatConversationSummaryDto(int Id, string? Title, string Model, DateTime UpdatedAt, bool IsOwnedByCaller, bool IsShared);

public record ChatConversationDetailDto(
    int Id,
    string? Title,
    string Model,
    bool IsOwnedByCaller,
    List<ChatMessageDto> Messages,
    List<int> SharedGroupIds);

/// <summary>Sharing is group-only in practice today (ChatConversationShare.SharedWithUserId exists but nothing in the UI ever sets it) — kept that way here rather than exposing an unused path.</summary>
public record UpdateChatSharesRequest(List<int> GroupIds);

public record SendChatMessageRequest(string Content, string? Model, bool WebSearchEnabled, bool DocsSearchEnabled);

public enum ChatStreamEventType { Token, Done, Error }

public record ChatStreamEvent(
    ChatStreamEventType Type,
    string? Token,
    int? ConversationId,
    string? ConversationTitle,
    ChatMessageDto? SavedMessage,
    string? WikiArticleCreatedTitle,
    string? ErrorMessage);
