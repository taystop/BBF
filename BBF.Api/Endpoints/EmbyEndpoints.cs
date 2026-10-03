using BBF.Api.Services;

namespace BBF.Api.Endpoints;

public static class EmbyEndpoints
{
    public static void MapEmbyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/integrations/emby").RequireAuthorization();

        group.MapGet("/system-info", async (EmbyService emby, CancellationToken ct) =>
        {
            var info = await emby.GetSystemInfoAsync(ct);
            return info is null ? Results.NotFound() : Results.Ok(info);
        });

        group.MapGet("/libraries", async (EmbyService emby, CancellationToken ct) =>
            Results.Ok(await emby.GetLibrariesAsync(ct)));

        group.MapGet("/item-counts", async (EmbyService emby, CancellationToken ct) =>
        {
            var counts = await emby.GetItemCountsAsync(ct);
            return counts is null ? Results.NotFound() : Results.Ok(counts);
        });

        group.MapGet("/sessions", async (EmbyService emby, CancellationToken ct) =>
            Results.Ok(await emby.GetActiveSessionsAsync(ct)));

        group.MapGet("/recent-items", async (int? limit, EmbyService emby, CancellationToken ct) =>
            Results.Ok(await emby.GetRecentItemsAsync(limit ?? 12, ct)));

        group.MapGet("/health", async (EmbyService emby, CancellationToken ct) =>
            Results.Ok(await emby.IsHealthyAsync(ct)));
    }
}
