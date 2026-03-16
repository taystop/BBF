namespace BBF.Data.Entities;

public class UserGroupMember
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = "Member"; // "Owner" or "Member"
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public UserGroup Group { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
