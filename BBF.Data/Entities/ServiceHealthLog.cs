namespace BBF.Data.Entities;

public class ServiceHealthLog
{
    public int Id { get; set; }
    public int ServiceLinkId { get; set; }
    public bool IsHealthy { get; set; }
    public int? ResponseTimeMs { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;

    public ServiceLink ServiceLink { get; set; } = null!;
}
