using BBF.Components;
using BBF.Components.Account;
using BBF.Data;
using BBF.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

// Ollama LLM service
builder.Services.AddHttpClient<OllamaService>();

// SearXNG web search service
builder.Services.AddHttpClient<WebSearchService>();

// Home Assistant integration
builder.Services.AddHttpClient<HomeAssistantService>();

// Health check service
builder.Services.AddScoped<HealthCheckService>();

// Emby media server integration
builder.Services.AddHttpClient<EmbyService>();

// AMP game server integration
builder.Services.AddHttpClient<AmpService>();

// Wiki RAG search service
builder.Services.AddScoped<WikiSearchService>();

// Document storage service
builder.Services.AddScoped<DocumentService>();

// Plaid banking integration
builder.Services.AddScoped<PlaidService>();

// User context (multi-tenancy)
builder.Services.AddScoped<UserContextService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Document download endpoint (behind auth)
app.MapGet("/api/documents/{id:int}/download", async (int id, DocumentService docs) =>
{
    var doc = await docs.GetByIdAsync(id);
    if (doc is null) return Results.NotFound();

    var path = docs.GetFilePath(doc);
    if (path is null) return Results.NotFound();

    return Results.File(path, doc.ContentType, doc.FileName);
}).RequireAuthorization();

// Plaid Link endpoints (behind auth)
app.MapPost("/api/plaid/create-link-token", async (PlaidService plaid, System.Security.Claims.ClaimsPrincipal user, ILogger<Program> logger) =>
{
    try
    {
        var userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "default";
        var token = await plaid.CreateLinkTokenAsync(userId);
        return Results.Ok(new { linkToken = token });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Plaid create-link-token failed");
        return Results.Problem(ex.Message);
    }
}).RequireAuthorization().DisableAntiforgery();

app.MapPost("/api/plaid/exchange-token", async (PlaidService plaid, PlaidExchangeRequest request) =>
{
    var connection = await plaid.ExchangePublicTokenAsync(request.PublicToken, request.InstitutionName, request.InstitutionId, request.GroupId);
    return Results.Ok(new { connectionId = connection.Id });
}).RequireAuthorization().DisableAntiforgery();

// One-time data migration endpoint: assigns existing data to admin user/default group
app.MapPost("/api/admin/migrate-tenancy", async (IDbContextFactory<ApplicationDbContext> dbFactory, System.Security.Claims.ClaimsPrincipal user) =>
{
    var userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (userId is null) return Results.Unauthorized();

    // Check if user is admin
    var userManager = app.Services.CreateScope().ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
    var appUser = await userManager.FindByIdAsync(userId);
    if (appUser is null || !await userManager.IsInRoleAsync(appUser, "Admin"))
        return Results.Forbid();

    await using var db = await dbFactory.CreateDbContextAsync();

    // Find or create user's first group
    var group = await db.UserGroupMembers
        .Where(m => m.UserId == userId)
        .Select(m => m.Group)
        .FirstOrDefaultAsync();

    if (group is null)
    {
        group = new BBF.Data.Entities.UserGroup { Name = $"{appUser.UserName}'s Budget", CreatedAt = DateTime.UtcNow };
        db.UserGroups.Add(group);
        await db.SaveChangesAsync();
        db.UserGroupMembers.Add(new BBF.Data.Entities.UserGroupMember
        {
            GroupId = group.Id, UserId = userId, Role = "Owner", JoinedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    // Assign unowned chat conversations to admin
    var unownedChats = await db.ChatConversations.Where(c => c.UserId == null).ToListAsync();
    foreach (var chat in unownedChats) chat.UserId = userId;

    // Assign ungrouped budget data to default group
    var unownedCategories = await db.BudgetCategories.Where(c => c.GroupId == null).ToListAsync();
    foreach (var cat in unownedCategories) cat.GroupId = group.Id;

    var unownedTransactions = await db.Transactions.Where(t => t.GroupId == null).ToListAsync();
    foreach (var txn in unownedTransactions) txn.GroupId = group.Id;

    var unownedConnections = await db.PlaidConnections.Where(p => p.GroupId == null).ToListAsync();
    foreach (var conn in unownedConnections) conn.GroupId = group.Id;

    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        chatsAssigned = unownedChats.Count,
        categoriesAssigned = unownedCategories.Count,
        transactionsAssigned = unownedTransactions.Count,
        connectionsAssigned = unownedConnections.Count,
        groupId = group.Id,
        groupName = group.Name
    });
}).RequireAuthorization().DisableAntiforgery();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();

public record PlaidExchangeRequest(string PublicToken, string InstitutionName, string InstitutionId, int? GroupId);
