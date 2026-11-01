using BBF.Shared.Contracts.Documents;
using Microsoft.AspNetCore.Components;

namespace BBF.Services;

/// <summary>Hands off to a real browser download via BBF.Web's own cookie-authenticated proxy endpoint (Program.cs's /api/documents/{id}/download) — unchanged from how this worked before Documents.razor moved onto IDocumentDownloadHandler.</summary>
public class BrowserDocumentDownloadHandler(NavigationManager navigationManager) : IDocumentDownloadHandler
{
    public Task DownloadAsync(DocumentDto document, int groupId, CancellationToken ct = default)
    {
        navigationManager.NavigateTo($"/api/documents/{document.Id}/download?groupId={groupId}", forceLoad: true);
        return Task.CompletedTask;
    }
}
