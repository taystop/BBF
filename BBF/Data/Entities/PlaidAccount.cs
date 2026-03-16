namespace BBF.Data.Entities;

public class PlaidAccount
{
    public int Id { get; set; }
    public int PlaidConnectionId { get; set; }
    public string PlaidAccountId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;         // Plaid's name (e.g., "Plaid Checking")
    public string? OfficialName { get; set; }                 // Bank's official name
    public string? CustomName { get; set; }                   // User-defined name (e.g., "CO Checking")
    public string? Mask { get; set; }                         // Last 4 digits
    public string? Type { get; set; }                         // "depository", "credit", etc.
    public string? Subtype { get; set; }                      // "checking", "savings", "credit card", etc.
    public bool IsActive { get; set; } = true;

    public PlaidConnection Connection { get; set; } = null!;

    /// <summary>
    /// Returns the best display name: custom name first, then official, then Plaid name.
    /// </summary>
    public string DisplayName => CustomName ?? OfficialName ?? Name;
}
