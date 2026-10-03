namespace BBF.Shared.Contracts.Documents;

public record DocumentDto(int Id, string FileName, string ContentType, long Size, string? Category, string? Description, DateTime UploadedAt);

public record UpdateDocumentRequest(string? Category, string? Description);
