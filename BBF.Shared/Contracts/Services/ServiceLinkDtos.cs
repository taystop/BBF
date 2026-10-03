namespace BBF.Shared.Contracts.Services;

public record ServiceHealthLogDto(int Id, bool IsHealthy, int? ResponseTimeMs, DateTime CheckedAt);

public record ServiceLinkDto(
    int Id,
    string Name,
    string Url,
    string? Icon,
    string? Category,
    string? Description,
    int SortOrder,
    string? HealthCheckUrl,
    bool IsActive,
    ServiceHealthLogDto? LastHealth,
    double? Uptime24h);

public record UpsertServiceLinkRequest(
    int Id,
    string Name,
    string Url,
    string? Icon,
    string? Category,
    string? Description,
    int SortOrder,
    string? HealthCheckUrl,
    bool IsActive);

public record RecentHealthLogDto(int ServiceLinkId, string ServiceLinkName, bool IsHealthy, int? ResponseTimeMs, DateTime CheckedAt);
