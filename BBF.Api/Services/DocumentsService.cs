using BBF.Api.Auth;
using BBF.Data.Entities;
using BBF.Shared.Contracts.Documents;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Services;

public class DocumentsService(ITenantDbContextFactory tenantDb, IConfiguration config)
{
    private readonly string _storagePath = config["Documents:StoragePath"]
        ?? Path.Combine(AppContext.BaseDirectory, "DocumentStorage");

    public async Task<List<DocumentDto>> GetAllAsync(int groupId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        return await db.Documents
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new DocumentDto(d.Id, d.FileName, d.ContentType, d.Size, d.Category, d.Description, d.UploadedAt))
            .ToListAsync(ct);
    }

    public async Task<DocumentDto> UploadAsync(
        int groupId, Stream fileStream, string fileName, string contentType,
        string? category = null, string? description = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_storagePath);

        var storedName = $"{Guid.NewGuid()}{Path.GetExtension(fileName)}";
        var filePath = Path.Combine(_storagePath, storedName);

        await using (var output = File.Create(filePath))
        {
            await fileStream.CopyToAsync(output, ct);
        }

        // Extract text content for RAG search
        var extractedText = DocumentTextExtractor.ExtractText(filePath);

        var fileInfo = new FileInfo(filePath);
        var doc = new Document
        {
            GroupId = groupId,
            FileName = fileName,
            StoredFileName = storedName,
            ContentType = contentType,
            Size = fileInfo.Length,
            Category = category,
            Description = description,
            ExtractedText = extractedText,
            UploadedAt = DateTime.UtcNow
        };

        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        db.Documents.Add(doc);
        await db.SaveChangesAsync(ct);
        return new DocumentDto(doc.Id, doc.FileName, doc.ContentType, doc.Size, doc.Category, doc.Description, doc.UploadedAt);
    }

    public async Task UpdateAsync(int groupId, int documentId, UpdateDocumentRequest request, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var entity = await db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (entity is null) return;

        entity.FileName = request.FileName;
        entity.Category = request.Category;
        entity.Description = request.Description;
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(int groupId, int documentId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (doc is null) return false;

        var path = Path.Combine(_storagePath, doc.StoredFileName);
        if (File.Exists(path))
            File.Delete(path);

        db.Documents.Remove(doc);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Returns the on-disk path for a document's content, verifying it belongs to the given group first. Used by the download endpoint.</summary>
    public async Task<(string FileName, string ContentType, string Path)?> GetDownloadInfoAsync(int groupId, int documentId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (doc is null) return null;

        var path = Path.Combine(_storagePath, doc.StoredFileName);
        return File.Exists(path) ? (doc.FileName, doc.ContentType, path) : null;
    }
}
