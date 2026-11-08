using BBF.Api.Auth;
using BBF.Data;
using BBF.Data.Entities;
using BBF.Shared.Contracts.Finance;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Services;

public class FinanceSettingsService(ITenantDbContextFactory tenantDb)
{
    public async Task<FinanceSettingsDto> GetAsync(int groupId, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        return new FinanceSettingsDto(await GetBudgetStartAsync(db, ct));
    }

    public async Task UpdateAsync(int groupId, UpdateFinanceSettingsRequest request, CancellationToken ct = default)
    {
        await using var db = await tenantDb.CreateForGroupAsync(groupId, ct);
        var settings = await db.FinanceSettings.FirstOrDefaultAsync(ct);
        if (settings is null)
        {
            settings = new FinanceSettings { GroupId = groupId };
            db.FinanceSettings.Add(settings);
        }
        settings.BudgetStartDate = request.BudgetStartDate?.Date;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>The group's budget start date, read through an already group-scoped context.</summary>
    public static Task<DateTime?> GetBudgetStartAsync(ApplicationDbContext db, CancellationToken ct) =>
        db.FinanceSettings.Select(f => f.BudgetStartDate).FirstOrDefaultAsync(ct);
}
