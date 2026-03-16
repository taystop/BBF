using BBF.Data.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BBF.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<ChatConversation> ChatConversations => Set<ChatConversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ChatConversationShare> ChatConversationShares => Set<ChatConversationShare>();
    public DbSet<ServiceLink> ServiceLinks => Set<ServiceLink>();
    public DbSet<WikiArticle> WikiArticles => Set<WikiArticle>();
    public DbSet<ServiceHealthLog> ServiceHealthLogs => Set<ServiceHealthLog>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<BudgetCategory> BudgetCategories => Set<BudgetCategory>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<PlaidConnection> PlaidConnections => Set<PlaidConnection>();
    public DbSet<PlaidAccount> PlaidAccounts => Set<PlaidAccount>();
    public DbSet<UserGroup> UserGroups => Set<UserGroup>();
    public DbSet<UserGroupMember> UserGroupMembers => Set<UserGroupMember>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppSetting>(e =>
        {
            e.HasKey(a => a.Key);
            e.Property(a => a.Key).HasMaxLength(100);
        });

        // ChatConversation -> ChatMessage (cascade delete)
        builder.Entity<ChatConversation>(e =>
        {
            e.HasMany(c => c.Messages)
             .WithOne(m => m.Conversation)
             .HasForeignKey(m => m.ConversationId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(c => c.Shares)
             .WithOne(s => s.Conversation)
             .HasForeignKey(s => s.ConversationId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(c => c.User)
             .WithMany(u => u.ChatConversations)
             .HasForeignKey(c => c.UserId)
             .OnDelete(DeleteBehavior.SetNull);
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

        // BudgetCategory -> Transaction (set null on delete)
        builder.Entity<BudgetCategory>(e =>
        {
            e.Property(b => b.MonthlyLimit).HasColumnType("decimal(18,2)");
            e.HasMany(b => b.Transactions)
             .WithOne(t => t.Category)
             .HasForeignKey(t => t.CategoryId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(b => b.Group)
             .WithMany()
             .HasForeignKey(b => b.GroupId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Transaction>(e =>
        {
            e.Property(t => t.Amount).HasColumnType("decimal(18,2)");
            e.HasIndex(t => t.PlaidTransactionId).IsUnique().HasFilter("[PlaidTransactionId] IS NOT NULL");
            e.HasIndex(t => t.Date);

            e.HasOne(t => t.Group)
             .WithMany()
             .HasForeignKey(t => t.GroupId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<PlaidConnection>(e =>
        {
            e.HasIndex(p => p.ItemId).IsUnique();

            e.HasOne(p => p.Group)
             .WithMany()
             .HasForeignKey(p => p.GroupId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasMany(p => p.Accounts)
             .WithOne(a => a.Connection)
             .HasForeignKey(a => a.PlaidConnectionId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PlaidAccount>(e =>
        {
            e.HasIndex(a => a.PlaidAccountId).IsUnique();
        });

        // UserGroup -> UserGroupMember (cascade delete)
        builder.Entity<UserGroup>(e =>
        {
            e.HasMany(g => g.Members)
             .WithOne(m => m.Group)
             .HasForeignKey(m => m.GroupId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserGroupMember>(e =>
        {
            e.HasIndex(m => new { m.GroupId, m.UserId }).IsUnique();

            e.HasOne(m => m.User)
             .WithMany(u => u.GroupMemberships)
             .HasForeignKey(m => m.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ChatConversationShare
        builder.Entity<ChatConversationShare>(e =>
        {
            e.HasOne(s => s.SharedWithGroup)
             .WithMany()
             .HasForeignKey(s => s.SharedWithGroupId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(s => s.SharedWithUser)
             .WithMany()
             .HasForeignKey(s => s.SharedWithUserId)
             .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
