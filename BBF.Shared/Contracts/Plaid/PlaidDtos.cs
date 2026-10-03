namespace BBF.Shared.Contracts.Plaid;

public record PlaidAccountDto(
    int Id, string PlaidAccountId, string Name, string? OfficialName, string? CustomName,
    string? Mask, string? Type, string? Subtype, bool IsActive, string DisplayName);

public record PlaidConnectionDto(
    int Id, string InstitutionName, string InstitutionId, DateTime? LastSynced, bool IsActive,
    List<PlaidAccountDto> Accounts);

public record ExchangePublicTokenRequest(string PublicToken, string InstitutionName, string InstitutionId);

public record UpdatePlaidAccountNameRequest(string CustomName);

public record SyncTransactionsResult(int Added);
