namespace BBF.Shared.Contracts.Emby;

public interface IEmbyService
{
    Task<EmbySystemInfo?> GetSystemInfoAsync(CancellationToken ct = default);
    Task<List<EmbyLibrary>> GetLibrariesAsync(CancellationToken ct = default);
    Task<EmbyItemCounts?> GetItemCountsAsync(CancellationToken ct = default);
    Task<List<EmbySession>> GetActiveSessionsAsync(CancellationToken ct = default);
    Task<List<EmbyItem>> GetRecentItemsAsync(int limit = 12, CancellationToken ct = default);
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}
