using System.Net.Http.Json;
using BBF.Shared.Contracts.Documents;

namespace BBF.Shared.ApiClients;

public class HttpDocumentService(HttpClient http) : IDocumentService
{
    public async Task<List<DocumentDto>> GetAllAsync(int groupId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<DocumentDto>>($"/api/groups/{groupId}/documents", ct) ?? [];

    public async Task<DocumentDto> UploadAsync(int groupId, Stream fileStream, string fileName, string contentType,
        string? category = null, string? description = null, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        using var content = new StreamContent(fileStream);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(content, "file", fileName);
        if (category is not null) form.Add(new StringContent(category), "category");
        if (description is not null) form.Add(new StringContent(description), "description");

        var response = await http.PostAsync($"/api/groups/{groupId}/documents", form, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
        return await response.Content.ReadFromJsonAsync<DocumentDto>(ct)
            ?? throw new InvalidOperationException("Empty response uploading document.");
    }

    public async Task UpdateAsync(int groupId, int documentId, UpdateDocumentRequest request, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"/api/groups/{groupId}/documents/{documentId}", request, ct);
        await response.EnsureSuccessWithErrorBodyAsync(ct);
    }

    public async Task<bool> DeleteAsync(int groupId, int documentId, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"/api/groups/{groupId}/documents/{documentId}", ct);
        return response.IsSuccessStatusCode;
    }
}
