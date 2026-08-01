using BBF.Data;
using BBF.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BBF.Services;

public class GoalService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public GoalService(IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<Goal>> GetGoalsAsync(int groupId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Goals
            .Include(g => g.Account)
            .Include(g => g.Contributions)
            .Where(g => g.GroupId == groupId)
            .OrderBy(g => g.Name)
            .ToListAsync();
    }

    public async Task CreateAsync(Goal goal)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.Goals.Add(goal);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(int goalId, string name, decimal targetAmount, DateTime? targetDate, int? accountId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.Goals.FindAsync(goalId);
        if (entity is null) return;

        entity.Name = name;
        entity.TargetAmount = targetAmount;
        entity.TargetDate = targetDate;
        entity.AccountId = accountId;
        await db.SaveChangesAsync();
    }

    public async Task SetActiveAsync(int goalId, bool isActive)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.Goals.FindAsync(goalId);
        if (entity is null) return;

        entity.IsActive = isActive;
        await db.SaveChangesAsync();
    }

    public async Task AddContributionAsync(int goalId, decimal amount, DateTime contributedAt, int? transactionId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.GoalContributions.Add(new GoalContribution
        {
            GoalId = goalId,
            Amount = amount,
            ContributedAt = contributedAt,
            TransactionId = transactionId
        });
        await db.SaveChangesAsync();
    }

    public async Task<decimal> GetProgressAsync(int goalId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.GoalContributions
            .Where(c => c.GoalId == goalId)
            .SumAsync(c => c.Amount);
    }
}
