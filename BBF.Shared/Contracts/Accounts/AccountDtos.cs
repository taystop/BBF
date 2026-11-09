namespace BBF.Shared.Contracts.Accounts;

public record AccountDto(
    int Id,
    string Name,
    string AccountType,
    decimal StartingBalance,
    decimal CurrentBalance,
    bool IsActive,
    int? PlaidAccountId,
    bool CountPurchasesInBudget = true,
    decimal? AvailableBalance = null,
    DateTime? BalanceAsOf = null,
    // Private to the signed-in user (they see everything; others don't).
    bool IsPrivate = false,
    // Private to another member: Name is "Private account" and balances are zero; read-only.
    bool IsHidden = false);

public record CreateAccountRequest(string Name, string AccountType, decimal StartingBalance, bool? CountPurchasesInBudget = null);

public record UpdateAccountRequest(string Name, string AccountType, int? PlaidAccountId, bool? CountPurchasesInBudget = null, bool? IsPrivate = null);
