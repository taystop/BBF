using BBF.Api.Services;
using BBF.Shared.Contracts.Documents;
using Microsoft.AspNetCore.Mvc;

namespace BBF.Api.Endpoints;

public static class DocumentsEndpoints
{
    public static void MapDocumentsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/groups/{groupId:int}/documents").RequireAuthorization();

        group.MapGet("/", async (int groupId, DocumentsService documents, CancellationToken ct) =>
            Results.Ok(await documents.GetAllAsync(groupId, ct)));

        group.MapPost("/", async (int groupId, [FromForm] IFormFile file, [FromForm] string? category, [FromForm] string? description, DocumentsService documents, CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            var result = await documents.UploadAsync(groupId, stream, file.FileName, file.ContentType, category, description, ct);
            return Results.Ok(result);
        }).DisableAntiforgery();

        group.MapPut("/{documentId:int}", async (int groupId, int documentId, UpdateDocumentRequest request, DocumentsService documents, CancellationToken ct) =>
        {
            await documents.UpdateAsync(groupId, documentId, request, ct);
            return Results.NoContent();
        });

        group.MapDelete("/{documentId:int}", async (int groupId, int documentId, DocumentsService documents, CancellationToken ct) =>
        {
            var deleted = await documents.DeleteAsync(groupId, documentId, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        group.MapGet("/{documentId:int}/download", async (int groupId, int documentId, DocumentsService documents, CancellationToken ct) =>
        {
            var info = await documents.GetDownloadInfoAsync(groupId, documentId, ct);
            return info is null ? Results.NotFound() : Results.File(info.Value.Path, info.Value.ContentType, info.Value.FileName);
        });
    }
}
