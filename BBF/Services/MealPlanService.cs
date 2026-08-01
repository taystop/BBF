using BBF.Data;
using BBF.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BBF.Services;

public class MealPlanService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public MealPlanService(IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    /// <summary>Weeks always start on Sunday — normalizes any date to that week's Sunday.</summary>
    public static DateTime GetWeekStart(DateTime date) => date.Date.AddDays(-(int)date.Date.DayOfWeek);

    public async Task<MealPlanWeek> GetOrCreateWeekAsync(int groupId, DateTime weekStartDate)
    {
        weekStartDate = GetWeekStart(weekStartDate);

        await using var db = await _dbFactory.CreateDbContextAsync();
        var week = await db.MealPlanWeeks
            .Include(w => w.Entries).ThenInclude(en => en.Recipe)
            .FirstOrDefaultAsync(w => w.GroupId == groupId && w.WeekStartDate == weekStartDate);

        if (week is null)
        {
            week = new MealPlanWeek { GroupId = groupId, WeekStartDate = weekStartDate };
            db.MealPlanWeeks.Add(week);
            await db.SaveChangesAsync();
        }

        return week;
    }

    public async Task AssignRecipeAsync(int weekId, DateTime date, string mealSlot, int recipeId, int plannedServings)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entry = await db.MealPlanEntries
            .FirstOrDefaultAsync(en => en.MealPlanWeekId == weekId && en.Date == date.Date && en.MealSlot == mealSlot);

        if (entry is null)
        {
            db.MealPlanEntries.Add(new MealPlanEntry
            {
                MealPlanWeekId = weekId,
                Date = date.Date,
                MealSlot = mealSlot,
                RecipeId = recipeId,
                PlannedServings = plannedServings
            });
        }
        else
        {
            entry.RecipeId = recipeId;
            entry.PlannedServings = plannedServings;
        }

        await db.SaveChangesAsync();
    }

    public async Task UnassignAsync(int entryId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.MealPlanEntries.FindAsync(entryId);
        if (entity is null) return;

        db.MealPlanEntries.Remove(entity);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Recipes used in the last 90 days (excluding the current week), most-recently-used first, for the
    /// "Recently Used" shortlist in the recipe picker — a deliberate alternative to bulk copy-week.
    /// Deduped and ordered in memory since SQL Server DISTINCT doesn't reliably preserve ORDER BY.
    /// </summary>
    public async Task<List<Recipe>> GetRecentlyUsedRecipesAsync(int groupId, int excludeWeekId, int take = 10)
    {
        var since = DateTime.UtcNow.Date.AddDays(-90);

        await using var db = await _dbFactory.CreateDbContextAsync();
        var entries = await db.MealPlanEntries
            .Include(en => en.Recipe)
            .Where(en => en.MealPlanWeek.GroupId == groupId && en.MealPlanWeekId != excludeWeekId && en.Date >= since)
            .OrderByDescending(en => en.Date)
            .ToListAsync();

        return entries
            .GroupBy(en => en.RecipeId)
            .OrderByDescending(g => g.Max(en => en.Date))
            .Select(g => g.First().Recipe)
            .Take(take)
            .ToList();
    }
}
