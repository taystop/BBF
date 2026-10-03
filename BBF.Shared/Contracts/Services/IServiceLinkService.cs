namespace BBF.Shared.Contracts.Services;

/// <summary>ServiceLink is global, not group-scoped — a shared household list of self-hosted services, same as the original.</summary>
public interface IServiceLinkService
{
    Task<List<ServiceLinkDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Active services that have a health check URL configured, each with up to 100 recent logs' worth of 24h uptime baked in.</summary>
    Task<List<ServiceLinkDto>> GetMonitoredAsync(CancellationToken ct = default);

    Task<List<RecentHealthLogDto>> GetRecentHealthLogsAsync(int take = 20, CancellationToken ct = default);
    Task<ServiceLinkDto> SaveAsync(UpsertServiceLinkRequest request, CancellationToken ct = default);
    Task DeleteAsync(int serviceLinkId, CancellationToken ct = default);
    Task<ServiceHealthLogDto> CheckHealthAsync(int serviceLinkId, CancellationToken ct = default);
}
