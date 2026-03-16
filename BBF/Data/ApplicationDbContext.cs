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
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<BudgetCategory> BudgetCategories => Set<BudgetCategory>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<PlaidConnection> PlaidConnections => Set<PlaidConnection>();

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

        builder.Entity<ServiceHealthLog>(e =>
        {
            e.ToTable("ServiceHealthLog");
        });

        builder.Entity<ServiceLink>(e =>
        {
            e.HasMany(s => s.HealthLogs)
             .WithOne(h => h.ServiceLink)
             .HasForeignKey(h => h.ServiceLinkId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BudgetCategory>(e =>
        {
            e.Property(b => b.MonthlyLimit).HasColumnType("decimal(18,2)");
            e.HasMany(b => b.Transactions)
             .WithOne(t => t.Category)
             .HasForeignKey(t => t.CategoryId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Transaction>(e =>
        {
            e.Property(t => t.Amount).HasColumnType("decimal(18,2)");
            e.HasIndex(t => t.PlaidTransactionId).IsUnique().HasFilter("[PlaidTransactionId] IS NOT NULL");
            e.HasIndex(t => t.Date);
        });

        builder.Entity<PlaidConnection>(e =>
        {
            e.HasIndex(p => p.ItemId).IsUnique();
        });
    }
}
