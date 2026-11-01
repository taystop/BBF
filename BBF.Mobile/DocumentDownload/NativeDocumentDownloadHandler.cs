using BBF.Shared.Contracts.Documents;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;
using MudBlazor;

namespace BBF.Mobile.DocumentDownload;

/// <summary>
/// BBF.Mobile has no browser chrome to hand a Content-Disposition response off to, so it pulls the
/// file itself via IDocumentService and hands it to Android's native share sheet — lets the user save
/// to Drive, "Save to device", open directly in another app, etc. Uses MAUI's cross-platform Share
/// API rather than hand-rolled FileProvider/Intent code.
/// </summary>
public class NativeDocumentDownloadHandler(IDocumentService documents, ISnackbar snackbar) : IDocumentDownloadHandler
{
    public async Task DownloadAsync(DocumentDto document, int groupId, CancellationToken ct = default)
    {
        var tempPath = Path.Combine(FileSystem.CacheDirectory, document.FileName);

        await using (var source = await documents.DownloadAsync(groupId, document.Id, ct))
        await using (var destination = File.Create(tempPath))
        {
            await source.CopyToAsync(destination, ct);
        }

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = $"Save {document.FileName}",
            File = new ShareFile(tempPath, document.ContentType)
        });

        snackbar.Add($"{document.FileName} ready to save", Severity.Success);
    }
}
