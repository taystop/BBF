namespace BBF.Data.Entities;

public class Document
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public string? ExtractedText { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
