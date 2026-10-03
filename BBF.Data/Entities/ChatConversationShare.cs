namespace BBF.Data.Entities;

public class ChatConversationShare
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public int? SharedWithGroupId { get; set; }
    public string? SharedWithUserId { get; set; }
    public DateTime SharedAt { get; set; } = DateTime.UtcNow;

    public ChatConversation Conversation { get; set; } = null!;
    public UserGroup? SharedWithGroup { get; set; }
    public ApplicationUser? SharedWithUser { get; set; }
}
