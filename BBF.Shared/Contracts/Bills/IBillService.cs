namespace BBF.Shared.Contracts.Bills;

public interface IBillService
{
    /// <summary>Bills due between <paramref name="from"/> and <paramref name="to"/> (inclusive), soonest first.</summary>
    Task<List<BillOccurrenceDto>> GetBillsAsync(int groupId, DateTime from, DateTime to, CancellationToken ct = default);

    Task UpdateStatusAsync(int groupId, int billId, UpdateBillStatusRequest request, CancellationToken ct = default);
}
