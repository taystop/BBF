using BBF.Api.Notifications;
using BBF.Api.Services;
using BBF.Data;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.BackgroundServices;

/// <summary>
/// The first concrete trigger against INotificationSender — everything else (budget alerts,
/// chat-reply pushes, ...) is just another call site against the same interface, no new plumbing.
/// Polls every monitored ServiceLink on a fixed interval and pushes a notification only on a
/// healthy/unhealthy transition (tracked in-memory per service), not on every failed check.
/// </summary>
public class ServiceHealthMonitorBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<ServiceHealthMonitorBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(2);
    private readonly Dictionary<int, bool> _lastKnownHealthy = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAllAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Service health monitoring pass failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task CheckAllAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        var links = scope.ServiceProvider.GetRequiredService<ServiceLinksService>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationSender>();

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var monitored = await db.ServiceLinks
            .Where(s => s.IsActive && s.HealthCheckUrl != null && s.HealthCheckUrl != "")
            .ToListAsync(ct);

        foreach (var service in monitored)
        {
            var result = await links.CheckHealthAsync(service.Id, ct);

            // Default "was healthy" so a service that's already down the first time we ever check it
            // still alerts, rather than silently adopting its current state as the baseline.
            var wasHealthy = _lastKnownHealthy.GetValueOrDefault(service.Id, true);
            _lastKnownHealthy[service.Id] = result.IsHealthy;

            if (wasHealthy && !result.IsHealthy)
            {
                await notifications.SendToAllAsync(new NotificationMessage(
                    "Service down",
                    $"{service.Name} is not responding.",
                    new Dictionary<string, string> { ["serviceLinkId"] = service.Id.ToString() }), ct);
            }
            else if (!wasHealthy && result.IsHealthy)
            {
                await notifications.SendToAllAsync(new NotificationMessage(
                    "Service back up",
                    $"{service.Name} is responding again.",
                    new Dictionary<string, string> { ["serviceLinkId"] = service.Id.ToString() }), ct);
            }
        }
    }
}
