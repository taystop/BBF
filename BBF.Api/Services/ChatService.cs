using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using BBF.Api.Auth;
using BBF.Data;
using BBF.Data.Entities;
using BBF.Shared.Contracts.Chat;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Services;

/// <summary>
/// Chat has no "active group" concept (conversations are owned by a user and optionally shared to
/// specific groups) — unlike every other domain so far, this deliberately does NOT go through
/// ITenantDbContextFactory/CurrentGroupId. It uses the plain, unscoped factory directly and replicates
/// the original Chat.razor's manual ownership/share checks, now centered on the JWT's user id.
/// </summary>
public class ChatService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserContext currentUser,
    OllamaService ollama,
    WebSearchService webSearch,
    WikiSearchService wikiSearch)
{
    private static readonly Regex WikiBlockRegex = new(
        @"\[WIKI_CREATE\]\s*\ntitle:\s*(.+)\ncategory:\s*(.+)\ntags:\s*(.+)\n---\n([\s\S]*?)\[/WIKI_CREATE\]",
        RegexOptions.Compiled);

    public async Task<List<ChatConversationSummaryDto>> GetConversationsAsync(CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var groupIds = await db.UserGroupMembers.Where(m => m.UserId == userId).Select(m => m.GroupId).ToListAsync(ct);

        var conversations = await db.ChatConversations
            .Include(c => c.Shares)
            .Where(c => c.UserId == userId
                || c.Shares.Any(s => s.SharedWithUserId == userId)
                || c.Shares.Any(s => s.SharedWithGroupId != null && groupIds.Contains(s.SharedWithGroupId.Value)))
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync(ct);

        return conversations
            .Select(c => new ChatConversationSummaryDto(c.Id, c.Title, c.Model, c.UpdatedAt, c.UserId == userId, c.UserId != userId))
            .ToList();
    }

    public async Task<ChatConversationDetailDto?> GetConversationAsync(int conversationId, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var conversation = await db.ChatConversations
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
            .Include(c => c.Shares)
            .FirstOrDefaultAsync(c => c.Id == conversationId, ct);
        if (conversation is null) return null;

        if (!await HasAccessAsync(db, conversation, userId, ct)) return null;

        return new ChatConversationDetailDto(
            conversation.Id, conversation.Title, conversation.Model, conversation.UserId == userId,
            conversation.Messages.Select(m => new ChatMessageDto(m.Id, m.Role, m.Content, m.CreatedAt)).ToList(),
            conversation.Shares.Where(s => s.SharedWithGroupId.HasValue).Select(s => s.SharedWithGroupId!.Value).ToList());
    }

    public async Task DeleteConversationAsync(int conversationId, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var convo = await db.ChatConversations.FirstOrDefaultAsync(c => c.Id == conversationId, ct);
        if (convo is null) return;
        if (convo.UserId != userId && convo.UserId is not null) return; // only the owner (or an unowned legacy convo) can delete

        db.ChatConversations.Remove(convo);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateSharesAsync(int conversationId, UpdateChatSharesRequest request, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var convo = await db.ChatConversations.Include(c => c.Shares).FirstOrDefaultAsync(c => c.Id == conversationId, ct);
        if (convo is null || convo.UserId != userId) return;

        var currentGroupIds = convo.Shares.Where(s => s.SharedWithGroupId.HasValue).Select(s => s.SharedWithGroupId!.Value).ToHashSet();
        var requestedGroupIds = request.GroupIds.ToHashSet();

        var toRemove = convo.Shares.Where(s => s.SharedWithGroupId.HasValue && !requestedGroupIds.Contains(s.SharedWithGroupId.Value)).ToList();
        db.ChatConversationShares.RemoveRange(toRemove);

        foreach (var groupId in requestedGroupIds.Where(id => !currentGroupIds.Contains(id)))
        {
            db.ChatConversationShares.Add(new ChatConversationShare
            {
                ConversationId = conversationId,
                SharedWithGroupId = groupId,
                SharedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
    }

    public Task<List<string>> GetAvailableModelsAsync(CancellationToken ct = default) => ollama.GetModelsAsync();

    public async IAsyncEnumerable<ChatStreamEvent> SendMessageStreamAsync(
        int? conversationId, SendChatMessageRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        var input = request.Content.Trim();

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        ChatConversation? conversation;
        if (conversationId is int existingId)
        {
            conversation = await db.ChatConversations.FirstOrDefaultAsync(c => c.Id == existingId, ct);
            if (conversation is null || !await HasAccessAsync(db, conversation, userId, ct))
            {
                yield return new ChatStreamEvent(ChatStreamEventType.Error, null, null, null, null, null, "Conversation not found.");
                yield break;
            }
        }
        else
        {
            conversation = new ChatConversation
            {
                Model = request.Model ?? "qwen3.5:9b",
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.ChatConversations.Add(conversation);
            await db.SaveChangesAsync(ct);
        }

        db.ChatMessages.Add(new ChatMessage { ConversationId = conversation.Id, Role = "user", Content = input, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);

        var history = await db.ChatMessages
            .Where(m => m.ConversationId == conversation.Id)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new OllamaChatMessage { Role = m.Role, Content = m.Content })
            .ToListAsync(ct);

        string? docsContext = null;
        string? webContext = null;
        if (request.DocsSearchEnabled)
        {
            var rag = await wikiSearch.SearchAllAsync(userId, input, maxResults: 5, ct);
            docsContext = WikiSearchService.FormatAsContext(rag);
        }
        if (request.WebSearchEnabled)
        {
            var results = await webSearch.SearchAsync(input, maxResults: 5, ct);
            webContext = WebSearchService.FormatResultsAsContext(results);
        }

        var contextParts = new List<string>();
        if (!string.IsNullOrEmpty(docsContext)) contextParts.Add(docsContext);
        if (!string.IsNullOrEmpty(webContext)) contextParts.Add(webContext);
        if (contextParts.Count > 0)
        {
            history[^1] = new OllamaChatMessage { Role = "user", Content = $"{input}\n\n---\n{string.Join("\n\n---\n", contextParts)}" };
        }

        var model = request.Model ?? conversation.Model;
        var streamed = new StringBuilder();
        var canceled = false;
        string? errorMessage = null;

        var enumerator = ollama.ChatStreamAsync(history, model, ct).GetAsyncEnumerator(ct);
        try
        {
            while (true)
            {
                var step = await AdvanceAsync(enumerator);
                if (step.Canceled) { canceled = true; break; }
                if (step.Error is not null) { errorMessage = step.Error; break; }
                if (!step.HasValue) break;

                if (step.Chunk!.IsThinking) continue;
                streamed.Append(step.Chunk.Text);
                yield return new ChatStreamEvent(ChatStreamEventType.Token, step.Chunk.Text, null, null, null, null, null);
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        if (errorMessage is not null)
        {
            // ConversationId is included even on error — the conversation (and the user's message) was
            // already saved above before the Ollama call, so a brand-new conversation must not be left
            // orphaned client-side just because generation itself failed.
            yield return new ChatStreamEvent(ChatStreamEventType.Error, null, conversation.Id, conversation.Title, null, null, errorMessage);
            yield break;
        }

        if (canceled && streamed.Length == 0)
            yield break; // nothing streamed before cancellation — nothing worth saving

        var finalContent = canceled ? streamed + "\n\n_(generation stopped)_" : streamed.ToString();

        var assistantMsg = new ChatMessage { ConversationId = conversation.Id, Role = "assistant", Content = finalContent, CreatedAt = DateTime.UtcNow };
        db.ChatMessages.Add(assistantMsg);
        conversation.UpdatedAt = DateTime.UtcNow;
        if (conversation.Title is null)
            conversation.Title = input.Length > 50 ? input[..50] + "..." : input;
        await db.SaveChangesAsync(ct);

        string? wikiTitle = canceled ? null : await ProcessWikiBlocksAsync(db, finalContent, ct);

        yield return new ChatStreamEvent(
            ChatStreamEventType.Done, null, conversation.Id, conversation.Title,
            new ChatMessageDto(assistantMsg.Id, assistantMsg.Role, assistantMsg.Content, assistantMsg.CreatedAt),
            wikiTitle, null);
    }

    private readonly record struct AdvanceResult(bool HasValue, ChatStreamChunk? Chunk, bool Canceled, string? Error);

    /// <summary>
    /// Isolates the try/catch around MoveNextAsync so the caller's loop body (which yields) only ever
    /// sits in a try/finally — C# disallows `yield return` inside a catch block.
    /// </summary>
    private static async Task<AdvanceResult> AdvanceAsync(IAsyncEnumerator<ChatStreamChunk> enumerator)
    {
        try
        {
            var hasNext = await enumerator.MoveNextAsync();
            return hasNext
                ? new AdvanceResult(true, enumerator.Current, false, null)
                : new AdvanceResult(false, null, false, null);
        }
        catch (OperationCanceledException)
        {
            return new AdvanceResult(false, null, true, null);
        }
        catch (HttpRequestException)
        {
            return new AdvanceResult(false, null, false, "Unable to reach Ollama. Make sure it's running at the configured address.");
        }
    }

    private static async Task<bool> HasAccessAsync(ApplicationDbContext db, ChatConversation conversation, string userId, CancellationToken ct)
    {
        if (conversation.UserId == userId || conversation.UserId is null) return true;

        var groupIds = await db.UserGroupMembers.Where(m => m.UserId == userId).Select(m => m.GroupId).ToListAsync(ct);
        return conversation.Shares.Any(s => s.SharedWithUserId == userId)
            || conversation.Shares.Any(s => s.SharedWithGroupId != null && groupIds.Contains(s.SharedWithGroupId.Value));
    }

    private static async Task<string?> ProcessWikiBlocksAsync(ApplicationDbContext db, string content, CancellationToken ct)
    {
        string? lastCreatedTitle = null;
        foreach (Match match in WikiBlockRegex.Matches(content))
        {
            var title = match.Groups[1].Value.Trim();
            var category = match.Groups[2].Value.Trim();
            var tags = match.Groups[3].Value.Trim();
            var body = match.Groups[4].Value.Trim();
            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(body)) continue;

            if (await db.WikiArticles.AnyAsync(a => a.Title == title, ct)) continue;

            db.WikiArticles.Add(new WikiArticle
            {
                Title = title,
                Content = body,
                Category = string.IsNullOrWhiteSpace(category) ? null : category,
                Tags = string.IsNullOrWhiteSpace(tags) ? null : tags,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
            lastCreatedTitle = title;
        }
        return lastCreatedTitle;
    }

    public async Task<string> SaveMessageToWikiAsync(int messageId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var msg = await db.ChatMessages.FirstOrDefaultAsync(m => m.Id == messageId, ct)
            ?? throw new KeyNotFoundException($"Chat message {messageId} not found.");

        var lines = msg.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var title = lines.FirstOrDefault()?.TrimStart('#', ' ') ?? "Untitled";
        if (title.Length > 80) title = title[..80] + "...";

        var content = WikiBlockRegex.Replace(msg.Content, "").Trim();

        if (await db.WikiArticles.AnyAsync(a => a.Title == title, ct))
            throw new InvalidOperationException($"Wiki article '{title}' already exists.");

        db.WikiArticles.Add(new WikiArticle
        {
            Title = title,
            Content = content,
            Category = "AI Generated",
            Tags = "chat,ai-generated",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
        return title;
    }
}
