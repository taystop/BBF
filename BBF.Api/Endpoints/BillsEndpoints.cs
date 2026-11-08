using BBF.Api.Services;
using BBF.Data.Entities;
using BBF.Shared.Contracts.Bills;

namespace BBF.Api.Endpoints;

public static class BillsEndpoints
{
    public static void MapBillsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/groups/{groupId:int}/bills").RequireAuthorization();

        group.MapGet("/", async (int groupId, DateTime? from, DateTime? to, BillsService bills, CancellationToken ct) =>
        {
            var start = from ?? DateTime.Today.AddDays(-30);
            var end = to ?? DateTime.Today.AddDays(45);
            if (end < start || (end - start).TotalDays > 400)
                return Results.BadRequest(new { error = "Invalid date range." });
            return Results.Ok(await bills.GetBillsAsync(groupId, start, end, ct));
        });

        group.MapPut("/{billId:int}/status", async (int groupId, int billId, UpdateBillStatusRequest request, BillsService bills, CancellationToken ct) =>
        {
            if (!BillStatuses.IsValid(request.Status))
                return Results.BadRequest(new { error = "Status must be Expected, Paid or Skipped." });
            await bills.UpdateStatusAsync(groupId, billId, request, ct);
            return Results.NoContent();
        });
    }
}
