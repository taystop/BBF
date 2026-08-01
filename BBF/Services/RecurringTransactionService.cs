using BBF.Data;
using BBF.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BBF.Services;

public class RecurringTransactionService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly TransactionService _transactionService;

    public RecurringTransactionService(IDbContextFactory<ApplicationDbContext> dbFactory, TransactionService transactionService)
    {
        _dbFactory = dbFactory;
        _transactionService = transactionService;
    }

    public async Task<List<RecurringTransaction>> GetActiveAsync(int groupId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.RecurringTransactions
            .Include(r => r.Category)
            .Include(r => r.Account)
            .Where(r => r.GroupId == groupId)
            .OrderBy(r => r.NextOccurrence)
            .ToListAsync();
    }

    public async Task CreateAsync(RecurringTransaction rule)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.RecurringTransactions.Add(rule);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(int ruleId, string payee, decimal amount, int? categoryId, int accountId,
        string frequency, DateTime nextOccurrence, DateTime? endDate, bool isActive)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.RecurringTransactions.FindAsync(ruleId);
        if (entity is null) return;

        entity.Payee = payee;
        entity.Amount = amount;
        entity.CategoryId = categoryId;
        entity.AccountId = accountId;
        entity.Frequency = frequency;
        entity.NextOccurrence = nextOccurrence;
        entity.EndDate = endDate;
        entity.IsActive = isActive;
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int ruleId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.RecurringTransactions.FindAsync(ruleId);
        if (entity is null) return;

        db.RecurringTransactions.Remove(entity);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Materializes any due recurring rules into real Transactions, advancing NextOccurrence past today
    /// (catching up on multiple missed occurrences if the group hasn't been visited in a while).
    /// Called from UserContextService.InitializeAsync since this app has no background job infrastructure.
    /// </summary>
    public async Task MaterializeDueAsync(int groupId)
    {
        var today = DateTime.UtcNow.Date;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var dueRules = await db.RecurringTransactions
            .Where(r => r.GroupId == groupId && r.IsActive && r.NextOccurrence <= today)
            .ToListAsync();

        foreach (var rule in dueRules)
        {
            var iterations = 0;
            while (rule.IsActive && rule.NextOccurrence <= today && iterations++ < 500)
            {
                await _transactionService.CreateAsync(new Transaction
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

                rule.NextOccurrence = Advance(rule.NextOccurrence, rule.Frequency);
                if (rule.EndDate is not null && rule.NextOccurrence > rule.EndDate)
                    rule.IsActive = false;
            }
        }

        await db.SaveChangesAsync();
    }

    private static DateTime Advance(DateTime date, string frequency) => frequency switch
    {
        "Weekly" => date.AddDays(7),
        "BiWeekly" => date.AddDays(14),
        "Yearly" => date.AddYears(1),
        _ => date.AddMonths(1), // "Monthly"
    };
}
