namespace BBF.Data.Entities;

public class UserGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<UserGroupMember> Members { get; set; } = [];
}
