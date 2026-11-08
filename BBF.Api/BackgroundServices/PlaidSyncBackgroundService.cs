using System.Threading.Channels;
using BBF.Api.Services;

namespace BBF.Api.BackgroundServices;

/// <summary>Connections waiting to sync — filled by the Plaid webhook, drained by <see cref="PlaidSyncBackgroundService"/>.</summary>
public class PlaidSyncQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();

    public void Enqueue(int connectionId) => _channel.Writer.TryWrite(connectionId);

    public ChannelReader<int> Reader => _channel.Reader;
}

/// <summary>
/// Keeps bank data current without paying for per-call Plaid endpoints: syncs a connection as soon as
/// Plaid's SYNC_UPDATES_AVAILABLE webhook arrives, plus a backup sync of every connection every 6 hours
/// (and shortly after startup) in case a webhook is missed. /transactions/sync is covered by the
/// monthly per-Item fee, so these extra calls cost nothing.
/// </summary>
public class PlaidSyncBackgroundService(
    PlaidSyncQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<PlaidSyncBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan BackupInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var nextBackup = DateTime.UtcNow + StartupDelay;

        while (!stoppingToken.IsCancellationRequested)
        {
            var untilBackup = nextBackup - DateTime.UtcNow;
            if (untilBackup <= TimeSpan.Zero)
            {
                await SyncAllAsync(stoppingToken);
                nextBackup = DateTime.UtcNow + BackupInterval;
                continue;
            }

            using var wait = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            wait.CancelAfter(untilBackup);
            try
            {
                var first = await queue.Reader.ReadAsync(wait.Token);

                // Several webhooks for the same bank often arrive together — sync each connection once.
                var batch = new HashSet<int> { first };
                while (queue.Reader.TryRead(out var more)) batch.Add(more);
                foreach (var id in batch)
                    await SyncOneAsync(id, "webhook", stoppingToken);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                // Backup timer elapsed; loop around and run it.
            }
        }
    }

    private async Task SyncAllAsync(CancellationToken ct)
    {
        List<int> ids;
        try
        {
            using var scope = scopeFactory.CreateScope();
            ids = await scope.ServiceProvider.GetRequiredService<PlaidService>().GetActiveConnectionIdsAsync(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Plaid backup sync could not list connections");
            return;
        }

        foreach (var id in ids)
            await SyncOneAsync(id, "backup", ct);
    }

    private async Task SyncOneAsync(int connectionId, string reason, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var added = await scope.ServiceProvider.GetRequiredService<PlaidService>().SyncConnectionAsTrustedAsync(connectionId, ct);
            logger.LogInformation("Plaid {Reason} sync of connection {ConnectionId}: {Added} new transactions", reason, connectionId, added);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Plaid {Reason} sync of connection {ConnectionId} failed", reason, connectionId);
        }
    }
}
