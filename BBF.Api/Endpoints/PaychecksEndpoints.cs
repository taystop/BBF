using BBF.Api.Services;
using BBF.Data.Entities;
using BBF.Shared.Contracts.Paychecks;

namespace BBF.Api.Endpoints;

public static class PaychecksEndpoints
{
    public static void MapPaychecksEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/groups/{groupId:int}").RequireAuthorization();

        group.MapGet("/pay-schedules", async (int groupId, PaychecksService paychecks, CancellationToken ct) =>
            Results.Ok(await paychecks.GetSchedulesAsync(groupId, ct)));

        group.MapPost("/pay-schedules", async (int groupId, SavePayScheduleRequest request, PaychecksService paychecks, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await paychecks.CreateScheduleAsync(groupId, request, ct));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPut("/pay-schedules/{scheduleId:int}", async (int groupId, int scheduleId, SavePayScheduleRequest request, PaychecksService paychecks, CancellationToken ct) =>
        {
            try
            {
                await paychecks.UpdateScheduleAsync(groupId, scheduleId, request, ct);
                return Results.NoContent();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapDelete("/pay-schedules/{scheduleId:int}", async (int groupId, int scheduleId, PaychecksService paychecks, CancellationToken ct) =>
        {
            await paychecks.DeleteScheduleAsync(groupId, scheduleId, ct);
            return Results.NoContent();
        });

        group.MapGet("/pay-periods", async (int groupId, DateTime? date, PaychecksService paychecks, CancellationToken ct) =>
            Results.Ok(await paychecks.GetPeriodAsync(groupId, date ?? DateTime.Today, ct)));

        group.MapGet("/paydays", async (int groupId, DateTime from, DateTime to, PaychecksService paychecks, CancellationToken ct) =>
        {
            if (to < from || (to - from).TotalDays > 400)
                return Results.BadRequest(new { error = "Invalid date range." });
            return Results.Ok(await paychecks.GetPaydaysAsync(groupId, from, to, ct));
        });

        group.MapPut("/paychecks/{paycheckId:int}/status", async (int groupId, int paycheckId, UpdatePaycheckStatusRequest request, PaychecksService paychecks, CancellationToken ct) =>
        {
            if (!PaycheckStatuses.IsValid(request.Status))
                return Results.BadRequest(new { error = "Status must be Expected, Received or Skipped." });
            await paychecks.UpdatePaycheckStatusAsync(groupId, paycheckId, request, ct);
            return Results.NoContent();
        });
    }
}
