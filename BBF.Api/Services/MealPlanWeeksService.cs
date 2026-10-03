using BBF.Api.Auth;
using BBF.Data.Entities;
using BBF.Shared.Contracts.MealPlanning;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Services;

public class MealPlanWeeksService(ITenantDbContextFactory tenantDb)
{
    /// <summary>Weeks always start on Sunday — normalizes any date to that week's Sunday.</summary>
    public static DateTime GetWeekStart(DateTime date) => date.Date.AddDays(-(int)date.Date.DayOfWeek);

    public async Task<MealPlanWeekDto> GetOrCreateWeekAsync(int groupId, DateTime weekStartDate, CancellationToken ct = default)
    {
        weekStartDate = GetWeekStart(weekStartDate);

        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var week = await db.MealPlanWeeks
            .Include(w => w.Entries).ThenInclude(en => en.Recipe)
            .FirstOrDefaultAsync(w => w.WeekStartDate == weekStartDate, ct);

        if (week is null)
        {
            week = new MealPlanWeek { GroupId = groupId, WeekStartDate = weekStartDate };
            db.MealPlanWeeks.Add(week);
            await db.SaveChangesAsync(ct);
        }

        return ToDto(week);
    }

    public async Task AssignRecipeAsync(int groupId, int weekId, AssignRecipeRequest request, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);

        // MealPlanEntry carries no GroupId of its own — confirm the week belongs to this tenant (the
        // query filter on MealPlanWeeks already enforces this) before touching its entries.
        var weekExists = await db.MealPlanWeeks.AnyAsync(w => w.Id == weekId, ct);
        if (!weekExists) return;

        var entry = await db.MealPlanEntries
            .FirstOrDefaultAsync(en => en.MealPlanWeekId == weekId && en.Date == request.Date.Date && en.MealSlot == request.MealSlot, ct);

        if (entry is null)
        {
            db.MealPlanEntries.Add(new MealPlanEntry
            {
                MealPlanWeekId = weekId,
                Date = request.Date.Date,
                MealSlot = request.MealSlot,
                RecipeId = request.RecipeId,
                PlannedServings = request.PlannedServings
            });
        }
        else
        {
            entry.RecipeId = request.RecipeId;
            entry.PlannedServings = request.PlannedServings;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task UnassignAsync(int groupId, int entryId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);

        // Required-navigation + filtered-parent trick: MealPlanWeek comes back null here both when the
        // entry doesn't exist AND when it belongs to another tenant (its week gets filtered out of the
        // Include) — either way, nothing to unassign.
        var entity = await db.MealPlanEntries.Include(en => en.MealPlanWeek).FirstOrDefaultAsync(en => en.Id == entryId, ct);
        if (entity?.MealPlanWeek is null) return;

        db.MealPlanEntries.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Recipes used in the last 90 days (excluding the current week), most-recently-used first. Rooted
    /// at MealPlanEntries (which has no GroupId filter of its own), so the tenant check here is a real,
    /// necessary manual join condition — not redundant with CurrentGroupId.
    /// </summary>
    public async Task<List<RecipeDto>> GetRecentlyUsedRecipesAsync(int groupId, int excludeWeekId, int take = 10, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.Date.AddDays(-90);

        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var entries = await db.MealPlanEntries
            .Include(en => en.Recipe)
            .Where(en => en.MealPlanWeek.GroupId == groupId && en.MealPlanWeekId != excludeWeekId && en.Date >= since)
            .OrderByDescending(en => en.Date)
            .ToListAsync(ct);

        return entries
            .GroupBy(en => en.RecipeId)
            .OrderByDescending(g => g.Max(en => en.Date))
            .Select(g => g.First().Recipe)
            .Take(take)
            .Select(r => new RecipeDto(r.Id, r.Name, r.BaseServings, r.Instructions, r.Notes, []))
            .ToList();
    }

    private static MealPlanWeekDto ToDto(MealPlanWeek w) => new(
        w.Id, w.WeekStartDate,
        w.Entries.Select(en => new MealPlanEntryDto(en.Id, en.Date, en.MealSlot, en.RecipeId, en.Recipe.Name, en.PlannedServings)).ToList());
}
