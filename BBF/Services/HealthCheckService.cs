using System.Diagnostics;
using BBF.Data;
using BBF.Data.Entities;

namespace BBF.Services;

public class HealthCheckService(ApplicationDbContext db)
{
    private static readonly HttpClient HttpClient = new(new HttpClientHandler
    {
        // Accept self-signed certs for internal services (e.g. EdgeRouter)
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true
    })
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

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

        db.ServiceHealthLogs.Add(log);
        await db.SaveChangesAsync();

        return log;
    }
}
