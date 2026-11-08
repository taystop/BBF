using BBF.Api.Services;
using BBF.Shared.Contracts.Finance;

namespace BBF.Api.Endpoints;

public static class FinanceSettingsEndpoints
{
    public static void MapFinanceSettingsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/groups/{groupId:int}/finance-settings").RequireAuthorization();

        group.MapGet("/", async (int groupId, FinanceSettingsService settings, CancellationToken ct) =>
            Results.Ok(await settings.GetAsync(groupId, ct)));

        group.MapPut("/", async (int groupId, UpdateFinanceSettingsRequest request, FinanceSettingsService settings, CancellationToken ct) =>
        {
            await settings.UpdateAsync(groupId, request, ct);
            return Results.NoContent();
        });
    }
}
