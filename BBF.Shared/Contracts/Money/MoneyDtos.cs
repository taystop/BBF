using BBF.Shared.Contracts.Alerts;
using BBF.Shared.Contracts.Paychecks;
using BBF.Shared.Contracts.Transactions;

namespace BBF.Shared.Contracts.Money;

/// <summary>
/// The Money home screen. SafeToSpend = checking balance + paychecks still expected this pay period −
/// bills still unpaid this pay period. Private items are included in every total.
/// </summary>
public record MoneySummaryDto(
    decimal SafeToSpend,
    decimal CheckingBalance,
    decimal IncomeStillExpected,
    decimal BillsStillDue,
    bool HasCheckingAccounts,
    DateTime? BalanceAsOf,
    DateTime? NextPayday,
    PayPeriodDto Period,
    List<UpcomingBillDto> NextBills,
    int ToCategorize,
    LeftoverPromptDto? LeftoverPrompt = null,
    int OpenAlerts = 0);

public record UpcomingBillDto(int BillId, string Payee, DateTime DueDate, decimal? Amount, bool IsHidden);

/// <summary>
/// Day-by-day checking balance: today's balance, then expected paychecks in and unpaid bills out.
/// Everyday spending isn't estimated.
/// </summary>
public record ForecastDto(
    decimal StartBalance,
    decimal LowBalanceThreshold,
    List<ForecastDayDto> Days);

public record ForecastDayDto(DateTime Date, List<ForecastItemDto> Items, decimal EndBalance, bool IsTight);

/// <summary>Kind is "Paycheck" or "Bill". Amount is signed (+ in, − out) and null when private to another member.</summary>
public record ForecastItemDto(string Kind, string Name, decimal? Amount, bool IsHidden);

public record CategorizeRequest(int CategoryId, bool RememberMerchant);

/// <summary>AlsoCategorized = other uncategorized transactions the new store rule filed too.</summary>
public record CategorizeResult(int AlsoCategorized, string? RuleText);

public record MerchantRuleDto(int Id, string DisplayText, int CategoryId, string CategoryName);

/// <summary>
/// A charge (or deposit, when IsIncome) seen on a steady schedule. Amount is positive. Accepting it
/// creates a recurring bill, or a pay schedule for income.
/// </summary>
public record RecurringSuggestionDto(
    string Key,
    string Payee,
    string? MatchText,
    decimal Amount,
    string Frequency,
    DateTime NextDue,
    int? CategoryId,
    string? CategoryName,
    int? AccountId,
    string? AccountName,
    List<DateTime> Dates,
    bool IsIncome);

public record DismissSuggestionRequest(string Key);

public interface IMoneyService
{
    Task<MoneySummaryDto> GetSummaryAsync(int groupId, CancellationToken ct = default);
    Task<ForecastDto> GetForecastAsync(int groupId, int days, CancellationToken ct = default);

    /// <summary>Spending that still needs a category, newest first.</summary>
    Task<List<TransactionDto>> GetToReviewAsync(int groupId, CancellationToken ct = default);
    Task<CategorizeResult> CategorizeAsync(int groupId, int transactionId, CategorizeRequest request, CancellationToken ct = default);
    Task<List<MerchantRuleDto>> GetMerchantRulesAsync(int groupId, CancellationToken ct = default);
    Task DeleteMerchantRuleAsync(int groupId, int ruleId, CancellationToken ct = default);

    Task<List<RecurringSuggestionDto>> GetSuggestionsAsync(int groupId, CancellationToken ct = default);
    Task DismissSuggestionAsync(int groupId, DismissSuggestionRequest request, CancellationToken ct = default);
}
