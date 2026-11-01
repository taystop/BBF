using BBF.Mobile.Auth;
using BBF.Shared;
using BBF.Shared.ApiClients;
using BBF.Shared.Contracts.Accounts;
using BBF.Shared.Contracts.Amp;
using BBF.Shared.Contracts.Auth;
using BBF.Shared.Contracts.Budget;
using BBF.Shared.Contracts.Chat;
using BBF.Shared.Contracts.Documents;
using BBF.Shared.Contracts.Emby;
using BBF.Shared.Contracts.Goals;
using BBF.Shared.Contracts.HomeAssistant;
using BBF.Shared.Contracts.Integrations;
using BBF.Shared.Contracts.MealPlanning;
using BBF.Shared.Contracts.Plaid;
using BBF.Shared.Contracts.Recurring;
using BBF.Shared.Contracts.Services;
using BBF.Shared.Contracts.Transactions;
using BBF.Shared.Contracts.Users;
using BBF.Shared.Contracts.Wiki;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;

namespace BBF.Mobile;

public static class MauiProgram
{
    // v1: a single hardcoded target, matching the one environment this app actually talks to.
    // Revisit if a dev/staging split is ever needed from the device itself.
    private const string ApiBaseUrl = "https://api.bigboisfederation.com";

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddMudServices();

        builder.Services.AddAuthorizationCore();
        builder.Services.AddCascadingAuthenticationState();

        // One instance plays both AuthenticationStateProvider and IApiAuthTokenProvider — see the
        // class comment on MobileAuthenticationStateProvider for why that's safe here (unlike
        // BBF.Web, there's no per-circuit scope to bridge).
        builder.Services.AddSingleton<MobileAuthenticationStateProvider>();
        builder.Services.AddSingleton<AuthenticationStateProvider>(sp => sp.GetRequiredService<MobileAuthenticationStateProvider>());
        builder.Services.AddSingleton<IApiAuthTokenProvider>(sp => sp.GetRequiredService<MobileAuthenticationStateProvider>());

        builder.Services.AddScoped<ApiGroupContext>();

        // Unauthenticated — used only to obtain the first token via /api/auth/login.
        builder.Services.AddHttpClient<IAuthService, HttpAuthService>(c => c.BaseAddress = new Uri(ApiBaseUrl));

        builder.Services.AddApiClientInfrastructure();
        builder.Services.AddApiHttpClient<IGroupService, HttpGroupService>(ApiBaseUrl);
        builder.Services.AddApiHttpClient<ITransactionService, HttpTransactionService>(ApiBaseUrl);
        builder.Services.AddApiHttpClient<IAccountService, HttpAccountService>(ApiBaseUrl);
        builder.Services.AddApiHttpClient<IGoalService, HttpGoalService>(ApiBaseUrl);
        builder.Services.AddApiHttpClient<IRecurringTransactionService, HttpRecurringTransactionService>(ApiBaseUrl);
        builder.Services.AddApiHttpClient<IBudgetCategoryService, HttpBudgetCategoryService>(ApiBaseUrl);
        builder.Services.AddApiHttpClient<IBudgetPeriodService, HttpBudgetPeriodService>(ApiBaseUrl);
        builder.Services.AddApiHttpClient<IPlaidService, HttpPlaidService>(ApiBaseUrl);
        builder.Services.AddApiHttpClient<IGrocerySectionService, HttpGrocerySectionService>(ApiBaseUrl);
        builder.Services.AddApiHttpClient<IIngredientService, HttpIngredientService>(ApiBaseUrl);
        builder.Services.AddApiHttpClient<IRecipeService, HttpRecipeService>(ApiBaseUrl);
        builder.Services.AddApiHttpClient<IMealPlanService, HttpMealPlanService>(ApiBaseUrl);
        builder.Services.AddApiHttpClient<IGroceryListService, HttpGroceryListService>(ApiBaseUrl);
        builder.Services.AddApiHttpClient<IDocumentService, HttpDocumentService>(ApiBaseUrl);
        builder.Services.AddApiHttpClient<IWikiArticleService, HttpWikiArticleService>(ApiBaseUrl);
        builder.Services.AddApiHttpClient<IChatService, HttpChatService>(ApiBaseUrl, TimeSpan.FromMinutes(5));
        builder.Services.AddApiHttpClient<IServiceLinkService, HttpServiceLinkService>(ApiBaseUrl);
        builder.Services.AddApiHttpClient<IHomeAssistantService, HttpHomeAssistantService>(ApiBaseUrl);
        builder.Services.AddApiHttpClient<IEmbyService, HttpEmbyService>(ApiBaseUrl);
        builder.Services.AddApiHttpClient<IAmpService, HttpAmpService>(ApiBaseUrl);
        builder.Services.AddApiHttpClient<IIntegrationsStatusService, HttpIntegrationsStatusService>(ApiBaseUrl);
        builder.Services.AddApiHttpClient<IUserManagementService, HttpUserManagementService>(ApiBaseUrl);

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
