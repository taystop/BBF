namespace BBF.Shared.Contracts.Documents;

/// <summary>Pure helpers the UI needs client-side (upload validation, display formatting) — no I/O, so no reason to round-trip to BBF.Api for these.</summary>
public static class DocumentHelpers
{
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

    public static bool IsAllowedExtension(string fileName) => AllowedExtensions.Contains(Path.GetExtension(fileName));

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
