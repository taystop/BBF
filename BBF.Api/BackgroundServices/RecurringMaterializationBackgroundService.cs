using BBF.Api.Services;
using BBF.Data;
using BBF.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.BackgroundServices;

/// <summary>
/// Replaces the old UserContextService side effect that materialized due recurring transactions only
/// when a user happened to visit their group — this app previously had no background job
/// infrastructure to do it any other way. Runs across every group's rules on a fixed interval instead,
/// using an unscoped DbContext (no CurrentGroupId set) since it is, by design, the one trusted
/// cross-tenant operation in the system.
/// </summary>
public class RecurringMaterializationBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<RecurringMaterializationBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await MaterializeAllDueAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Recurring transaction materialization failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task MaterializeAllDueAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();

        var today = DateTime.UtcNow.Date;
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var dueRules = await db.RecurringTransactions
            .Where(r => r.IsActive && r.NextOccurrence <= today)
            .ToListAsync(ct);

        foreach (var rule in dueRules)
        {
            var iterations = 0;
            while (rule.IsActive && rule.NextOccurrence <= today && iterations++ < 500)
            {
                db.Transactions.Add(new Transaction
                {
                    Amount = rule.Amount,
                    Date = rule.NextOccurrence,
                    Description = rule.Payee,
                    CategoryId = rule.CategoryId,
                    AccountId = rule.AccountId,
                    GroupId = rule.GroupId,
                    Source = "Recurring",
                    RecurringTransactionId = rule.Id,
                    CreatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync(ct);

                await AccountsService.RecalculateBalanceAsync(rule.AccountId, db);

                rule.NextOccurrence = Advance(rule.NextOccurrence, rule.Frequency);
                if (rule.EndDate is not null && rule.NextOccurrence > rule.EndDate)
                    rule.IsActive = false;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static DateTime Advance(DateTime date, string frequency) => frequency switch
    {
        "Weekly" => date.AddDays(7),
        "BiWeekly" => date.AddDays(14),
        "Yearly" => date.AddYears(1),
        _ => date.AddMonths(1), // "Monthly"
    };
}
