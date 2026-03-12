using BBF.Data.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BBF.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<ChatConversation> ChatConversations => Set<ChatConversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ServiceLink> ServiceLinks => Set<ServiceLink>();
    public DbSet<WikiArticle> WikiArticles => Set<WikiArticle>();
    public DbSet<ServiceHealthLog> ServiceHealthLogs => Set<ServiceHealthLog>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppSetting>(e =>
        {
            e.HasKey(a => a.Key);
            e.Property(a => a.Key).HasMaxLength(100);
        });

        builder.Entity<ChatConversation>(e =>
        {
            e.HasMany(c => c.Messages)
             .WithOne(m => m.Conversation)
             .HasForeignKey(m => m.ConversationId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ServiceLink>(e =>
        {
            e.HasMany(s => s.HealthLogs)
             .WithOne(h => h.ServiceLink)
             .HasForeignKey(h => h.ServiceLinkId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
