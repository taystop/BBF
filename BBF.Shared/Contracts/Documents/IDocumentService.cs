namespace BBF.Shared.Contracts.Documents;

public interface IDocumentService
{
    Task<List<DocumentDto>> GetAllAsync(int groupId, CancellationToken ct = default);
    Task<DocumentDto> UploadAsync(int groupId, Stream fileStream, string fileName, string contentType, string? category = null, string? description = null, CancellationToken ct = default);
    Task UpdateAsync(int groupId, int documentId, UpdateDocumentRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int groupId, int documentId, CancellationToken ct = default);
}
