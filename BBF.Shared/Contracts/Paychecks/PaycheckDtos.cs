namespace BBF.Shared.Contracts.Paychecks;

/// <summary>
/// A pay schedule. For a private schedule seen by someone other than its earner, ExpectedAmount,
/// DepositAccountName and MatchText are null and IsMine is false.
/// </summary>
public record PayScheduleDto(
    int Id,
    string UserId,
    string EarnerName,
    string Name,
    decimal? ExpectedAmount,
    string Frequency,
    DateTime FirstPayday,
    int? DepositAccountId,
    string? DepositAccountName,
    string? MatchText,
    bool IsPrivate,
    bool IsActive,
    bool IsMine,
    DateTime? NextPayday);

public record SavePayScheduleRequest(
    string UserId,
    string Name,
    decimal ExpectedAmount,
    string Frequency,
    DateTime FirstPayday,
    int? DepositAccountId,
    string? MatchText,
    bool IsPrivate,
    bool IsActive = true);

/// <summary>One payday. Amounts are null when the paycheck is private to another member.</summary>
public record PaycheckDto(
    int Id,
    int PayScheduleId,
    string Name,
    string EarnerName,
    DateTime PayDate,
    decimal? ExpectedAmount,
    decimal? ActualAmount,
    string Status,
    bool IsHidden,
    string? DepositDescription)
{
    /// <summary>Overtime or other extra pay: what landed above the normal amount.</summary>
    public decimal? Extra => ActualAmount is decimal a && ExpectedAmount is decimal e && a - e >= 0.01m ? a - e : null;
}

/// <summary>A bill as it sits in a pay period. Payee and Amount are hidden for another member's private account.</summary>
public record PeriodBillDto(
    int BillId,
    string Payee,
    DateTime DueDate,
    decimal? Amount,
    string Status,
    bool IsMoved,
    bool IsHidden);

/// <summary>
/// One pay period: from a payday up to the day before the next payday of any earner. Totals include
/// private items even when their details are hidden. Positive numbers throughout; Leftover can go negative.
/// </summary>
public record PayPeriodDto(
    DateTime Start,
    DateTime End,
    bool HasPaySchedules,
    List<PaycheckDto> Paychecks,
    List<PeriodBillDto> Bills,
    decimal Income,
    decimal IncomeReceived,
    decimal BillsTotal,
    decimal BillsPaid,
    decimal OtherSpending,
    decimal Leftover,
    // Still to happen this period (private items included): paychecks not yet in, bills not yet paid.
    decimal IncomeStillExpected = 0,
    decimal BillsStillDue = 0);

public record UpdatePaycheckStatusRequest(string Status);

/// <summary>Moves a bill to the pay period starting on PayDate. Null puts it back on its default payday.</summary>
public record MoveBillRequest(DateTime? PayDate);

public interface IPaycheckService
{
    Task<List<PayScheduleDto>> GetSchedulesAsync(int groupId, CancellationToken ct = default);
    Task<PayScheduleDto> CreateScheduleAsync(int groupId, SavePayScheduleRequest request, CancellationToken ct = default);
    Task UpdateScheduleAsync(int groupId, int scheduleId, SavePayScheduleRequest request, CancellationToken ct = default);
    Task DeleteScheduleAsync(int groupId, int scheduleId, CancellationToken ct = default);

    /// <summary>The pay period containing <paramref name="date"/>.</summary>
    Task<PayPeriodDto> GetPeriodAsync(int groupId, DateTime date, CancellationToken ct = default);

    /// <summary>Distinct paydays (any earner) between two dates, for choosing where a bill is paid from.</summary>
    Task<List<DateTime>> GetPaydaysAsync(int groupId, DateTime from, DateTime to, CancellationToken ct = default);

    Task UpdatePaycheckStatusAsync(int groupId, int paycheckId, UpdatePaycheckStatusRequest request, CancellationToken ct = default);
    Task MoveBillAsync(int groupId, int billId, MoveBillRequest request, CancellationToken ct = default);
}
