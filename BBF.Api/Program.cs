using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using BBF.Api.Auth;
using BBF.Api.BackgroundServices;
using BBF.Api.Endpoints;
using BBF.Api.Services;
using BBF.Data;
using BBF.Data.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

// Cloudflare Tunnel terminates on localhost, so Kestrel sees every request as coming from 127.0.0.1 —
// rate limiting by RemoteIpAddress alone would bucket every real client together. Fall back to it only
// when CF-Connecting-IP is absent (local/dev/direct LAN calls), the same trust model AuthEndpoints
// already uses for the internal-token block.
static string GetClientIp(HttpContext context) =>
    context.Request.Headers["CF-Connecting-IP"].FirstOrDefault()
    ?? context.Connection.RemoteIpAddress?.ToString()
    ?? "unknown";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlServer(connectionString));

// Same Identity store as BBF.Web (Stores.SchemaVersion must match exactly, or `dotnet ef migrations`
// run with this project as startup will diff against the wrong schema — see BBF/Program.cs).
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

var jwtSigningKey = builder.Configuration["Jwt:SigningKey"];
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey ?? string.Empty)),
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Loose, per-IP safety net across the whole API now that it's reachable from the open internet.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(GetClientIp(context), _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = 300,
            QueueLimit = 0,
        }));

    // Tighter policy for /api/auth — this is the actual brute-force/credential-stuffing target.
    // Identity's per-account lockout (5 attempts/5 min) only slows guessing one account at a time;
    // this caps total attempts per source IP across any number of usernames.
    options.AddPolicy<string>("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(GetClientIp(context), _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = 10,
            QueueLimit = 0,
        }));
});

builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();
builder.Services.AddScoped<ITenantDbContextFactory, TenantDbContextFactory>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<TransactionsService>();
builder.Services.AddScoped<AccountsService>();
builder.Services.AddScoped<BudgetCategoriesService>();
builder.Services.AddScoped<BudgetPeriodsService>();
builder.Services.AddScoped<RecurringTransactionsService>();
builder.Services.AddScoped<GoalsService>();
builder.Services.AddScoped<GrocerySectionsService>();
builder.Services.AddScoped<IngredientsService>();
builder.Services.AddScoped<RecipesService>();
builder.Services.AddScoped<MealPlanWeeksService>();
builder.Services.AddScoped<GroceryListsService>();
builder.Services.AddScoped<DocumentsService>();
builder.Services.AddScoped<WikiArticlesService>();
builder.Services.AddScoped<WikiSearchService>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<GroupManagementService>();
builder.Services.AddScoped<ServiceLinksService>();
builder.Services.AddScoped<UserManagementService>();
builder.Services.AddScoped<PlaidService>();
builder.Services.AddHttpClient<OllamaService>();
builder.Services.AddHttpClient<WebSearchService>();
builder.Services.AddHttpClient<HomeAssistantService>();
builder.Services.AddHttpClient<EmbyService>();
builder.Services.AddHttpClient<AmpService>();
builder.Services.AddHostedService<RecurringMaterializationBackgroundService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Anonymous — used by the deploy pipeline's smoke test and available for future uptime monitoring.
// No DB/LAN-service calls here on purpose, so it reflects only "is the process up and routing requests."
app.MapGet("/health", () => Results.Ok("healthy"));

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var statusCode = exception switch
        {
            ForbiddenAccessException => StatusCodes.Status403Forbidden,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            InvalidOperationException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };
        context.Response.StatusCode = statusCode;

        // The real exception detail is never sent to the client (it can contain SQL/file-path/stack
        // detail) — it only ever goes to this DB log, so it stays recoverable for diagnosis.
        if (exception is not null)
        {
            try
            {
                var dbFactory = context.RequestServices.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
                await using var db = await dbFactory.CreateDbContextAsync(CancellationToken.None);
                db.ErrorLogs.Add(new ErrorLog
                {
                    StatusCode = statusCode,
                    Method = context.Request.Method,
                    Path = context.Request.Path.Value ?? "",
                    ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
                    ExceptionMessage = exception.Message,
                    StackTrace = exception.StackTrace,
                    UserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier),
                });
                await db.SaveChangesAsync(CancellationToken.None);
            }
            catch
            {
                // Logging the error must never mask the original failure or crash the error handler itself.
            }
        }

        var genericMessage = statusCode switch
        {
            StatusCodes.Status403Forbidden => "Forbidden.",
            StatusCodes.Status404NotFound => "Not found.",
            StatusCodes.Status409Conflict => "Conflict.",
            _ => "An unexpected error occurred.",
        };
        await context.Response.WriteAsJsonAsync(new { error = genericMessage });
    });
});

app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapGroupsEndpoints();
app.MapTransactionsEndpoints();
app.MapAccountsEndpoints();
app.MapBudgetCategoriesEndpoints();
app.MapBudgetEndpoints();
app.MapRecurringTransactionsEndpoints();
app.MapGoalsEndpoints();
app.MapGrocerySectionsEndpoints();
app.MapIngredientsEndpoints();
app.MapRecipesEndpoints();
app.MapMealPlanEndpoints();
app.MapGroceryListsEndpoints();
app.MapDocumentsEndpoints();
app.MapWikiArticlesEndpoints();
app.MapChatEndpoints();
app.MapServiceLinksEndpoints();
app.MapHomeAssistantEndpoints();
app.MapEmbyEndpoints();
app.MapAmpEndpoints();
app.MapIntegrationsStatusEndpoints();
app.MapPlaidEndpoints();
app.MapUsersEndpoints();

app.Run();
