using BBF.ApiClients;
using BBF.Components;
using BBF.Components.Account;
using BBF.Data;
using BBF.Services;
using BBF.Shared;
using BBF.Shared.Contracts.Accounts;
using BBF.Shared.Contracts.Amp;
using BBF.Shared.Contracts.Auth;
using BBF.Shared.Contracts.Budget;
using BBF.Shared.Contracts.Chat;
using BBF.Shared.Contracts.Documents;
using BBF.Shared.Contracts.Emby;
using BBF.Shared.Contracts.HomeAssistant;
using BBF.Shared.Contracts.Integrations;
using BBF.Shared.Contracts.MealPlanning;
using BBF.Shared.Contracts.Plaid;
using BBF.Shared.Contracts.Services;
using BBF.Shared.Contracts.Transactions;
using BBF.Shared.Contracts.Users;
using BBF.Shared.Contracts.Wiki;
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

// SearXNG web search service
builder.Services.AddHttpClient<WebSearchService>();

// Health check service
builder.Services.AddScoped<HealthCheckService>();

// AMP game server integration — still used directly here only by the anonymous /servers page
// (PublicServers.razor), which can't carry a Bearer token and so can't go through BBF.Api.
builder.Services.AddHttpClient<AmpService>();

// Wiki RAG search service
builder.Services.AddScoped<WikiSearchService>();

// Document storage service
builder.Services.AddScoped<DocumentService>();

// Plaid banking integration
builder.Services.AddScoped<PlaidService>();

// Manual account ledger + balance maintenance
builder.Services.AddScoped<AccountService>();

// Transaction CRUD + CSV import (keeps account balance recalculation in one place)
builder.Services.AddScoped<TransactionService>();

// Zero-based budget periods, line items, and rollover
builder.Services.AddScoped<BudgetPeriodService>();

// Recurring transaction rules + login-triggered materialization
builder.Services.AddScoped<RecurringTransactionService>();

// Savings goals + contributions
builder.Services.AddScoped<GoalService>();

// User context (multi-tenancy)
builder.Services.AddScoped<UserContextService>();

// Meal planning: sections/ingredients maintenance, recipes, weekly plan, grocery list generation
builder.Services.AddScoped<GrocerySectionService>();
builder.Services.AddScoped<IngredientService>();
builder.Services.AddScoped<RecipeService>();
builder.Services.AddScoped<MealPlanService>();
builder.Services.AddScoped<GroceryListService>();

// --- BBF.Api client infrastructure (Phase 2) ---
// Additive for now: registers the HTTP-backed implementations pages will switch to as they move into
// BBF.Shared, alongside (not instead of) the direct-EF services above, which still back every page
// that hasn't moved yet. Nothing above this block changes behavior.
var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5001";
builder.Services.AddApiClientInfrastructure(apiBaseUrl);
builder.Services.AddApiHttpClient<IGroupService, HttpGroupService>(apiBaseUrl);
builder.Services.AddApiHttpClient<ITransactionService, HttpTransactionService>(apiBaseUrl);
builder.Services.AddApiHttpClient<IAccountService, HttpAccountService>(apiBaseUrl);
builder.Services.AddApiHttpClient<IBudgetCategoryService, HttpBudgetCategoryService>(apiBaseUrl);
builder.Services.AddApiHttpClient<IPlaidService, HttpPlaidService>(apiBaseUrl);
builder.Services.AddApiHttpClient<IGrocerySectionService, HttpGrocerySectionService>(apiBaseUrl);
builder.Services.AddApiHttpClient<IIngredientService, HttpIngredientService>(apiBaseUrl);
builder.Services.AddApiHttpClient<IRecipeService, HttpRecipeService>(apiBaseUrl);
builder.Services.AddApiHttpClient<IMealPlanService, HttpMealPlanService>(apiBaseUrl);
builder.Services.AddApiHttpClient<IGroceryListService, HttpGroceryListService>(apiBaseUrl);
builder.Services.AddApiHttpClient<IDocumentService, HttpDocumentService>(apiBaseUrl);
builder.Services.AddApiHttpClient<IWikiArticleService, HttpWikiArticleService>(apiBaseUrl);
// Longer timeout than the default 100s HttpClient allows — a long LLM response streaming through
// BBF.Api shouldn't get cut off mid-generation.
builder.Services.AddApiHttpClient<IChatService, HttpChatService>(apiBaseUrl, TimeSpan.FromMinutes(5));
builder.Services.AddApiHttpClient<IServiceLinkService, HttpServiceLinkService>(apiBaseUrl);
builder.Services.AddApiHttpClient<IHomeAssistantService, HttpHomeAssistantService>(apiBaseUrl);
builder.Services.AddApiHttpClient<IEmbyService, HttpEmbyService>(apiBaseUrl);
builder.Services.AddApiHttpClient<IAmpService, HttpAmpService>(apiBaseUrl);
builder.Services.AddApiHttpClient<IIntegrationsStatusService, HttpIntegrationsStatusService>(apiBaseUrl);
builder.Services.AddApiHttpClient<IUserManagementService, HttpUserManagementService>(apiBaseUrl);
builder.Services.AddScoped<ApiGroupContext>();

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
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(BBF.Shared.ApiGroupContext).Assembly);

// Document download endpoint (behind auth) — a plain browser <a> link can't carry a bearer token, so
// this proxies the request to BBF.Api server-side using the circuit's bridged JWT, then streams the
// response straight back. Kept at this exact path since existing hrefs point here.
app.MapGet("/api/documents/{id:int}/download", async (
    int id, int groupId, IHttpClientFactory httpClientFactory, IApiAuthTokenProvider tokenProvider, IConfiguration config) =>
{
    var token = await tokenProvider.GetTokenAsync();
    using var client = httpClientFactory.CreateClient("BBF.Api.Internal");
    using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/groups/{groupId}/documents/{id}/download");
    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
    if (!response.IsSuccessStatusCode) return Results.NotFound();

    var stream = await response.Content.ReadAsStreamAsync();
    var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
    var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
        ?? response.Content.Headers.ContentDisposition?.FileName
        ?? "download";
    return Results.Stream(stream, contentType, fileName);
}).RequireAuthorization();

// Plaid Link endpoints (behind auth) — kept at these exact paths because wwwroot/js/plaid-link.js
// fetches them directly (same-origin, no bearer token needed from the browser's point of view); they
// now proxy to BBF.Api's IPlaidService instead of calling EF directly.
app.MapPost("/api/plaid/create-link-token", async (IPlaidService plaid, ILogger<Program> logger) =>
{
    try
    {
        var token = await plaid.CreateLinkTokenAsync();
        return Results.Ok(new { linkToken = token });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Plaid create-link-token failed");
        return Results.Problem(ex.Message);
    }
}).RequireAuthorization().DisableAntiforgery();

app.MapPost("/api/plaid/exchange-token", async (IPlaidService plaid, PlaidExchangeRequest request) =>
{
    if (request.GroupId is null) return Results.BadRequest("groupId is required.");

    var connection = await plaid.ExchangePublicTokenAsync(
        request.GroupId.Value,
        new ExchangePublicTokenRequest(request.PublicToken, request.InstitutionName, request.InstitutionId));
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
