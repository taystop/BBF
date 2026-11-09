namespace BBF.Data.Entities;

/// <summary>A recurring-charge or paycheck suggestion someone marked "not recurring", so it stops coming back.</summary>
public class SuggestionDismissal
{
    public int Id { get; set; }
    public int? GroupId { get; set; }
    public UserGroup? Group { get; set; }

    /// <summary>Identifies the suggestion: direction, normalized payee and frequency.</summary>
    public string Key { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
