namespace BBF.Data.Entities;

/// <summary>
/// "Always file this store under this category": applied to new bank transactions whose description,
/// merchant or bank text contains MatchText, and to existing uncategorized ones when the rule is made.
/// </summary>
public class MerchantRule
{
    public int Id { get; set; }
    public int? GroupId { get; set; }
    public UserGroup? Group { get; set; }

    /// <summary>Normalized (lowercase letters and digits only) text to look for.</summary>
    public string MatchText { get; set; } = string.Empty;

    /// <summary>What the person typed or the merchant name it came from, for display.</summary>
    public string DisplayText { get; set; } = string.Empty;

    public int CategoryId { get; set; }
    public BudgetCategory Category { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
