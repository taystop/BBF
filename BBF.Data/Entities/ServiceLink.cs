namespace BBF.Data.Entities;

public class ServiceLink
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public string? HealthCheckUrl { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ServiceHealthLog> HealthLogs { get; set; } = [];
}
