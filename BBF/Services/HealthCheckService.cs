using System.Diagnostics;
using BBF.Data;
using BBF.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BBF.Services;

public class HealthCheckService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    private static readonly HttpClient HttpClient = new(new HttpClientHandler
    {
        // Accept self-signed certs for internal services (e.g. EdgeRouter)
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true
    })
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public HealthCheckService(IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<ServiceHealthLog> CheckAsync(ServiceLink svc)
    {
        var log = new ServiceHealthLog
        {
            ServiceLinkId = svc.Id,
            CheckedAt = DateTime.UtcNow
        };

        if (string.IsNullOrEmpty(svc.HealthCheckUrl))
        {
            log.IsHealthy = false;
            return log;
        }

        try
        {
            var sw = Stopwatch.StartNew();
            var response = await HttpClient.GetAsync(svc.HealthCheckUrl);
            sw.Stop();

            log.IsHealthy = response.IsSuccessStatusCode;
            log.ResponseTimeMs = (int)sw.ElapsedMilliseconds;
        }
        catch
        {
            log.IsHealthy = false;
        }

        await using var db = await _dbFactory.CreateDbContextAsync();
        db.ServiceHealthLogs.Add(log);
        await db.SaveChangesAsync();

        return log;
    }
}
