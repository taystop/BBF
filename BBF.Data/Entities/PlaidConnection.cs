namespace BBF.Data.Entities;

public class PlaidConnection
{
    public int Id { get; set; }
    public string InstitutionName { get; set; } = string.Empty;
    public string InstitutionId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string? Cursor { get; set; }
    public DateTime? LastSynced { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? GroupId { get; set; }
    public UserGroup? Group { get; set; }

    public ICollection<PlaidAccount> Accounts { get; set; } = [];
}
