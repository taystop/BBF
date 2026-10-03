namespace BBF.Shared.Contracts.Accounts;

public record AccountDto(
    int Id,
    string Name,
    string AccountType,
    decimal StartingBalance,
    decimal CurrentBalance,
    bool IsActive,
    int? PlaidAccountId);

public record CreateAccountRequest(string Name, string AccountType, decimal StartingBalance);

public record UpdateAccountRequest(string Name, string AccountType, int? PlaidAccountId);
