using BBF.Api.Services;
using BBF.Data;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.BackgroundServices;

/// <summary>
/// Every 15 minutes, across every group: posts due transactions for AutoPost rules, creates expected
/// bills for the rest (see <see cref="BillMatching"/>) and expected paychecks (see
/// <see cref="PaycheckMatching"/>), and matches both to transactions that have arrived. Uses an unscoped DbContext — the one trusted cross-tenant operation here; matching
/// compares GroupId explicitly so groups never mix.
/// </summary>
public class RecurringBillsBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<RecurringBillsBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Recurring bills job failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var today = DateTime.Today;
        await BillMatching.GenerateAsync(db, today, ct);
        await PaycheckMatching.GenerateAsync(db, today, ct);
        await db.SaveChangesAsync(ct);

        // Paychecks first, so a deposit is never taken by an income-type bill rule.
        await PaycheckMatching.MatchAsync(db, today, ct);
        await db.SaveChangesAsync(ct);
        await BillMatching.MatchAsync(db, today, ct);
        await db.SaveChangesAsync(ct);
    }
}
