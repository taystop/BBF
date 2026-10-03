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
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<BudgetPeriod> BudgetPeriods => Set<BudgetPeriod>();
    public DbSet<BudgetLineItem> BudgetLineItems => Set<BudgetLineItem>();
    public DbSet<RecurringTransaction> RecurringTransactions => Set<RecurringTransaction>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<GoalContribution> GoalContributions => Set<GoalContribution>();
    public DbSet<UserGroup> UserGroups => Set<UserGroup>();
    public DbSet<UserGroupMember> UserGroupMembers => Set<UserGroupMember>();
    public DbSet<GrocerySection> GrocerySections => Set<GrocerySection>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<MealPlanWeek> MealPlanWeeks => Set<MealPlanWeek>();
    public DbSet<MealPlanEntry> MealPlanEntries => Set<MealPlanEntry>();
    public DbSet<GroceryList> GroceryLists => Set<GroceryList>();
    public DbSet<GroceryListItem> GroceryListItems => Set<GroceryListItem>();

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

            // Restrict, not SetNull/Cascade — SQL Server rejects this self-reference combined with the
            // other cascading paths already into BudgetCategories (Transactions, BudgetLineItems).
            // Deleting a category with subcategories must be guarded in application code (see Budget.razor).
            e.HasOne(b => b.ParentCategory)
             .WithMany(b => b.Subcategories)
             .HasForeignKey(b => b.ParentCategoryId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Transaction>(e =>
        {
            e.Property(t => t.Amount).HasColumnType("decimal(18,2)");
            e.HasIndex(t => t.PlaidTransactionId).IsUnique().HasFilter("[PlaidTransactionId] IS NOT NULL");
            e.HasIndex(t => t.Date);
            e.HasIndex(t => t.AccountId);

            e.HasOne(t => t.Group)
             .WithMany()
             .HasForeignKey(t => t.GroupId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(t => t.RecurringTransaction)
             .WithMany()
             .HasForeignKey(t => t.RecurringTransactionId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // Account -> Transaction (set null on delete), Account -> PlaidAccount (optional reconciliation link)
        builder.Entity<Account>(e =>
        {
            e.Property(a => a.StartingBalance).HasColumnType("decimal(18,2)");
            e.Property(a => a.CurrentBalance).HasColumnType("decimal(18,2)");

            e.HasOne(a => a.Group)
             .WithMany()
             .HasForeignKey(a => a.GroupId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(a => a.PlaidAccount)
             .WithMany()
             .HasForeignKey(a => a.PlaidAccountId)
             .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(a => a.PlaidAccountId).IsUnique().HasFilter("[PlaidAccountId] IS NOT NULL");

            e.HasMany(a => a.Transactions)
             .WithOne(t => t.Account)
             .HasForeignKey(t => t.AccountId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // BudgetPeriod -> BudgetLineItem (cascade delete)
        builder.Entity<BudgetPeriod>(e =>
        {
            e.HasIndex(p => new { p.GroupId, p.StartDate }).IsUnique();

            e.HasOne(p => p.Group)
             .WithMany()
             .HasForeignKey(p => p.GroupId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasMany(p => p.LineItems)
             .WithOne(li => li.BudgetPeriod)
             .HasForeignKey(li => li.BudgetPeriodId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BudgetLineItem>(e =>
        {
            e.Property(li => li.BudgetedAmount).HasColumnType("decimal(18,2)");
            e.Property(li => li.RolledOverAmount).HasColumnType("decimal(18,2)");
            e.HasIndex(li => new { li.BudgetPeriodId, li.CategoryId }).IsUnique();

            // Restrict, not Cascade/SetNull: BudgetPeriod already cascades into this table (SQL Server
            // rejects two cascade paths), and CategoryId is non-nullable so SetNull isn't valid either.
            // Category deletion must be guarded in application code once line items exist (see Budget.razor).
            e.HasOne(li => li.Category)
             .WithMany(c => c.LineItems)
             .HasForeignKey(li => li.CategoryId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // RecurringTransaction -> Account (restrict — can't delete an account a rule still targets)
        builder.Entity<RecurringTransaction>(e =>
        {
            e.Property(r => r.Amount).HasColumnType("decimal(18,2)");

            e.HasOne(r => r.Group)
             .WithMany()
             .HasForeignKey(r => r.GroupId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(r => r.Category)
             .WithMany()
             .HasForeignKey(r => r.CategoryId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(r => r.Account)
             .WithMany()
             .HasForeignKey(r => r.AccountId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // Goal -> GoalContribution (cascade delete)
        builder.Entity<Goal>(e =>
        {
            e.Property(g => g.TargetAmount).HasColumnType("decimal(18,2)");

            e.HasOne(g => g.Group)
             .WithMany()
             .HasForeignKey(g => g.GroupId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(g => g.Account)
             .WithMany()
             .HasForeignKey(g => g.AccountId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasMany(g => g.Contributions)
             .WithOne(c => c.Goal)
             .HasForeignKey(c => c.GoalId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<GoalContribution>(e =>
        {
            e.Property(c => c.Amount).HasColumnType("decimal(18,2)");

            e.HasOne(c => c.Transaction)
             .WithMany()
             .HasForeignKey(c => c.TransactionId)
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

        // GrocerySection -> Ingredient (set null on delete)
        builder.Entity<GrocerySection>(e =>
        {
            e.HasOne(s => s.Group)
             .WithMany()
             .HasForeignKey(s => s.GroupId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasMany(s => s.Ingredients)
             .WithOne(i => i.Section)
             .HasForeignKey(i => i.SectionId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Ingredient>(e =>
        {
            e.Property(i => i.PackageQuantity).HasColumnType("decimal(10,3)");

            e.HasOne(i => i.Group)
             .WithMany()
             .HasForeignKey(i => i.GroupId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // Recipe -> RecipeIngredient (cascade delete, owned by the recipe)
        builder.Entity<Recipe>(e =>
        {
            e.HasOne(r => r.Group)
             .WithMany()
             .HasForeignKey(r => r.GroupId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasMany(r => r.Ingredients)
             .WithOne(ri => ri.Recipe)
             .HasForeignKey(ri => ri.RecipeId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RecipeIngredient>(e =>
        {
            e.Property(ri => ri.Quantity).HasColumnType("decimal(10,3)");

            // Restrict, not SetNull: Recipe already cascades into this table (SQL Server rejects two
            // cascade paths), and IngredientId is non-nullable. Must remove the line from recipes
            // before deleting the ingredient — guarded in application code.
            e.HasOne(ri => ri.Ingredient)
             .WithMany(i => i.RecipeIngredients)
             .HasForeignKey(ri => ri.IngredientId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // MealPlanWeek -> MealPlanEntry (cascade delete), MealPlanWeek -> GroceryList (cascade delete)
        builder.Entity<MealPlanWeek>(e =>
        {
            e.HasIndex(w => new { w.GroupId, w.WeekStartDate }).IsUnique();

            e.HasOne(w => w.Group)
             .WithMany()
             .HasForeignKey(w => w.GroupId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasMany(w => w.Entries)
             .WithOne(en => en.MealPlanWeek)
             .HasForeignKey(en => en.MealPlanWeekId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(w => w.GroceryList)
             .WithOne(gl => gl.MealPlanWeek)
             .HasForeignKey<GroceryList>(gl => gl.MealPlanWeekId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MealPlanEntry>(e =>
        {
            e.HasIndex(en => new { en.MealPlanWeekId, en.Date, en.MealSlot }).IsUnique();

            // Restrict: MealPlanWeek already cascades into this table. Must unschedule before
            // deleting a recipe — guarded in application code.
            e.HasOne(en => en.Recipe)
             .WithMany()
             .HasForeignKey(en => en.RecipeId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // GroceryList -> GroceryListItem (cascade delete)
        builder.Entity<GroceryList>(e =>
        {
            e.HasMany(gl => gl.Items)
             .WithOne(i => i.GroceryList)
             .HasForeignKey(i => i.GroceryListId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<GroceryListItem>(e =>
        {
            e.Property(i => i.RequiredQuantity).HasColumnType("decimal(10,3)");

            e.HasOne(i => i.Ingredient)
             .WithMany()
             .HasForeignKey(i => i.IngredientId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(i => i.Section)
             .WithMany()
             .HasForeignKey(i => i.SectionId)
             .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
