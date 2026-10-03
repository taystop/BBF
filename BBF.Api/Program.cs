using System.Text;
using BBF.Api.Auth;
using BBF.Api.BackgroundServices;
using BBF.Api.Endpoints;
using BBF.Api.Services;
using BBF.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

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
builder.Services.AddAuthorization();

builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();
builder.Services.AddScoped<ITenantDbContextFactory, TenantDbContextFactory>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<TransactionsService>();
builder.Services.AddScoped<AccountsService>();
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
builder.Services.AddHttpClient<OllamaService>();
builder.Services.AddHttpClient<WebSearchService>();
builder.Services.AddHostedService<RecurringMaterializationBackgroundService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        context.Response.StatusCode = exception is ForbiddenAccessException
            ? StatusCodes.Status403Forbidden
            : StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new { error = exception?.Message });
    });
});

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapGroupsEndpoints();
app.MapTransactionsEndpoints();
app.MapAccountsEndpoints();
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

app.Run();
