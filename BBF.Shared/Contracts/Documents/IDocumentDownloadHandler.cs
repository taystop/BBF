namespace BBF.Shared.Contracts.Documents;

/// <summary>
/// Triggers an actual download/save of a document — deliberately host-specific, unlike every other
/// document operation. BBF.Web can rely on a real browser (anchor + Content-Disposition does the
/// work); BBF.Mobile has no browser chrome to hand that off to, so it needs to pull the bytes itself
/// via IDocumentService and hand them to a native save/share flow instead.
/// </summary>
public interface IDocumentDownloadHandler
{
    Task DownloadAsync(DocumentDto document, int groupId, CancellationToken ct = default);
}
