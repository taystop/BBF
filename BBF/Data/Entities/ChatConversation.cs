namespace BBF.Data.Entities;

public class ChatConversation
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string Model { get; set; } = "qwen3.5:9b";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ChatMessage> Messages { get; set; } = [];
}
