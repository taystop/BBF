using System.Diagnostics;
using BBF.Data;
using BBF.Data.Entities;
using BBF.Shared.Contracts.Services;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Services;

/// <summary>ServiceLink/ServiceHealthLog are global, not group-scoped — a shared household list of self-hosted services. Folds in the old standalone HealthCheckService.</summary>
public class ServiceLinksService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    private static readonly HttpClient HealthCheckHttpClient = new(new HttpClientHandler
    {
        // Accept self-signed certs for internal services (e.g. EdgeRouter)
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true
    })
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public async Task<List<ServiceLinkDto>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var services = await db.ServiceLinks
            .Include(s => s.HealthLogs.OrderByDescending(h => h.CheckedAt).Take(1))
            .OrderBy(s => s.SortOrder)
            .ToListAsync(ct);

        return services.Select(s => ToDto(s, s.HealthLogs.FirstOrDefault(), null)).ToList();
    }

    public async Task<List<ServiceLinkDto>> GetMonitoredAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var services = await db.ServiceLinks
            .Include(s => s.HealthLogs.OrderByDescending(h => h.CheckedAt).Take(100))
            .Where(s => s.IsActive && s.HealthCheckUrl != null && s.HealthCheckUrl != "")
            .OrderBy(s => s.SortOrder)
            .ToListAsync(ct);

        var cutoff = DateTime.UtcNow.AddHours(-24);
        return services.Select(s =>
        {
            var last = s.HealthLogs.OrderByDescending(h => h.CheckedAt).FirstOrDefault();
            var recent = s.HealthLogs.Where(h => h.CheckedAt >= cutoff).ToList();
            double? uptime = recent.Count == 0 ? null : (double)recent.Count(h => h.IsHealthy) / recent.Count * 100;
            return ToDto(s, last, uptime);
        }).ToList();
    }

    public async Task<List<RecentHealthLogDto>> GetRecentHealthLogsAsync(int take = 20, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var logs = await db.ServiceHealthLogs
            .Include(h => h.ServiceLink)
            .OrderByDescending(h => h.CheckedAt)
            .Take(take)
            .ToListAsync(ct);

        return logs.Select(h => new RecentHealthLogDto(h.ServiceLinkId, h.ServiceLink.Name, h.IsHealthy, h.ResponseTimeMs, h.CheckedAt)).ToList();
    }

    public async Task<ServiceLinkDto> SaveAsync(UpsertServiceLinkRequest request, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        ServiceLink entity;
        if (request.Id == 0)
        {
            entity = new ServiceLink();
            db.ServiceLinks.Add(entity);
        }
        else
        {
            entity = await db.ServiceLinks.FirstOrDefaultAsync(s => s.Id == request.Id, ct)
                ?? throw new KeyNotFoundException($"Service link {request.Id} not found.");
        }

        entity.Name = request.Name;
        entity.Url = request.Url;
        entity.Icon = request.Icon;
        entity.Category = request.Category;
        entity.Description = request.Description;
        entity.SortOrder = request.SortOrder;
        entity.HealthCheckUrl = request.HealthCheckUrl;
        entity.IsActive = request.IsActive;

        await db.SaveChangesAsync(ct);
        return ToDto(entity, null, null);
    }

    public async Task DeleteAsync(int serviceLinkId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var entity = await db.ServiceLinks.FirstOrDefaultAsync(s => s.Id == serviceLinkId, ct);
        if (entity is null) return;

        db.ServiceLinks.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task<ServiceHealthLogDto> CheckHealthAsync(int serviceLinkId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var svc = await db.ServiceLinks.FirstOrDefaultAsync(s => s.Id == serviceLinkId, ct)
            ?? throw new KeyNotFoundException($"Service link {serviceLinkId} not found.");

        var log = new ServiceHealthLog { ServiceLinkId = svc.Id, CheckedAt = DateTime.UtcNow };

        if (string.IsNullOrEmpty(svc.HealthCheckUrl))
        {
            log.IsHealthy = false;
        }
        else
        {
            try
            {
                var sw = Stopwatch.StartNew();
                var response = await HealthCheckHttpClient.GetAsync(svc.HealthCheckUrl, ct);
                sw.Stop();

                log.IsHealthy = response.IsSuccessStatusCode;
                log.ResponseTimeMs = (int)sw.ElapsedMilliseconds;
            }
            catch
            {
                log.IsHealthy = false;
            }
        }

        db.ServiceHealthLogs.Add(log);
        await db.SaveChangesAsync(ct);

        return new ServiceHealthLogDto(log.Id, log.IsHealthy, log.ResponseTimeMs, log.CheckedAt);
    }

    private static ServiceLinkDto ToDto(ServiceLink s, ServiceHealthLog? lastHealth, double? uptime24h) => new(
        s.Id, s.Name, s.Url, s.Icon, s.Category, s.Description, s.SortOrder, s.HealthCheckUrl, s.IsActive,
        lastHealth is null ? null : new ServiceHealthLogDto(lastHealth.Id, lastHealth.IsHealthy, lastHealth.ResponseTimeMs, lastHealth.CheckedAt),
        uptime24h);
}
