namespace BBF.Shared.Contracts.Chat;

public interface IChatService
{
    Task<List<ChatConversationSummaryDto>> GetConversationsAsync(CancellationToken ct = default);
    Task<ChatConversationDetailDto?> GetConversationAsync(int conversationId, CancellationToken ct = default);
    Task DeleteConversationAsync(int conversationId, CancellationToken ct = default);
    Task UpdateSharesAsync(int conversationId, UpdateChatSharesRequest request, CancellationToken ct = default);
    Task<List<string>> GetAvailableModelsAsync(CancellationToken ct = default);

    /// <summary>Streams one assistant reply. Pass null conversationId to start a new conversation — the first Done event carries its id.</summary>
    IAsyncEnumerable<ChatStreamEvent> SendMessageStreamAsync(int? conversationId, SendChatMessageRequest request, CancellationToken ct = default);

    /// <summary>Saves a chat message's content as a new wiki article, returning its title. Throws if a same-titled article already exists.</summary>
    Task<string> SaveMessageToWikiAsync(int messageId, CancellationToken ct = default);
}
