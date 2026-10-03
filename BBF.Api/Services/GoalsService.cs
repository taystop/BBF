using BBF.Api.Auth;
using BBF.Data.Entities;
using BBF.Shared.Contracts.Goals;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Services;

public class GoalsService(ITenantDbContextFactory tenantDb)
{
    public async Task<List<GoalDto>> GetGoalsAsync(int groupId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var goals = await db.Goals
            .Include(g => g.Account)
            .Include(g => g.Contributions)
            .OrderBy(g => g.Name)
            .ToListAsync(ct);

        return goals.Select(ToDto).ToList();
    }

    public async Task<GoalDto> CreateAsync(int groupId, CreateGoalRequest request, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var goal = new Goal
        {
            GroupId = groupId,
            Name = request.Name,
            TargetAmount = request.TargetAmount,
            TargetDate = request.TargetDate,
            AccountId = request.AccountId,
        };
        db.Goals.Add(goal);
        await db.SaveChangesAsync(ct);

        await db.Entry(goal).Reference(g => g.Account).LoadAsync(ct);
        return ToDto(goal);
    }

    public async Task UpdateAsync(int groupId, int goalId, UpdateGoalRequest request, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var entity = await db.Goals.FirstOrDefaultAsync(g => g.Id == goalId, ct);
        if (entity is null) return;

        entity.Name = request.Name;
        entity.TargetAmount = request.TargetAmount;
        entity.TargetDate = request.TargetDate;
        entity.AccountId = request.AccountId;
        await db.SaveChangesAsync(ct);
    }

    public async Task SetActiveAsync(int groupId, int goalId, bool isActive, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var entity = await db.Goals.FirstOrDefaultAsync(g => g.Id == goalId, ct);
        if (entity is null) return;

        entity.IsActive = isActive;
        await db.SaveChangesAsync(ct);
    }

    public async Task AddContributionAsync(int groupId, int goalId, AddGoalContributionRequest request, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);

        // GoalContribution carries no GroupId of its own — confirm the goal belongs to this tenant
        // (the query filter on Goals already enforces this) before attaching a contribution to it.
        var goalExists = await db.Goals.AnyAsync(g => g.Id == goalId, ct);
        if (!goalExists) return;

        db.GoalContributions.Add(new GoalContribution
        {
            GoalId = goalId,
            Amount = request.Amount,
            ContributedAt = request.ContributedAt,
            TransactionId = request.TransactionId
        });
        await db.SaveChangesAsync(ct);
    }

    private static GoalDto ToDto(Goal g) => new(
        g.Id, g.Name, g.AccountId, g.Account?.Name, g.TargetAmount, g.TargetDate, g.IsActive,
        g.Contributions.Sum(c => c.Amount),
        g.Contributions.Select(c => new GoalContributionDto(c.Id, c.Amount, c.ContributedAt, c.TransactionId)).ToList());
}
