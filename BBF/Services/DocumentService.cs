using BBF.Data;
using BBF.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BBF.Services;

public class DocumentService(ApplicationDbContext db, IConfiguration config)
{
    private readonly string _storagePath = config["Documents:StoragePath"]
        ?? Path.Combine(AppContext.BaseDirectory, "DocumentStorage");

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Images
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg", ".ico",
        // Documents
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".odt", ".ods", ".odp",
        // Text
        ".txt", ".md", ".csv", ".json", ".xml", ".yaml", ".yml",
        ".log", ".ini", ".conf", ".cfg",
        // Archives
        ".zip", ".7z", ".tar", ".gz",
        // Other
        ".html", ".css", ".js"
    };

    public string StoragePath => _storagePath;

    public bool IsAllowedExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return AllowedExtensions.Contains(ext);
    }

    public async Task<Document> UploadAsync(Stream fileStream, string fileName, string contentType,
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
            FileName = fileName,
            StoredFileName = storedName,
            ContentType = contentType,
            Size = fileInfo.Length,
            Category = category,
            Description = description,
            ExtractedText = extractedText,
            UploadedAt = DateTime.UtcNow
        };

        db.Documents.Add(doc);
        await db.SaveChangesAsync(ct);
        return doc;
    }

    public async Task<List<Document>> GetAllAsync(CancellationToken ct = default)
    {
        return await db.Documents
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync(ct);
    }

    public async Task<Document?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await db.Documents.FindAsync([id], ct);
    }

    public string? GetFilePath(Document doc)
    {
        var path = Path.Combine(_storagePath, doc.StoredFileName);
        return File.Exists(path) ? path : null;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var doc = await db.Documents.FindAsync([id], ct);
        if (doc is null) return false;

        var path = Path.Combine(_storagePath, doc.StoredFileName);
        if (File.Exists(path))
            File.Delete(path);

        db.Documents.Remove(doc);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task UpdateAsync(Document doc, CancellationToken ct = default)
    {
        db.Documents.Update(doc);
        await db.SaveChangesAsync(ct);
    }

    public static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        var order = 0;
        var size = (double)bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}
