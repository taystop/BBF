using Microsoft.Extensions.DependencyInjection;

namespace BBF.Shared.ApiClients;

public static class ApiClientServiceCollectionExtensions
{
    /// <summary>
    /// Registers the handler every BBF.Api-backed typed client attaches for its Bearer token. Each host
    /// (BBF.Web, BBF.Mobile) must separately register its own IApiAuthTokenProvider implementation —
    /// Web exchanges its cookie session for a token via the internal-token endpoint; Mobile reads one
    /// from SecureStorage after a direct password login.
    /// </summary>
    public static IServiceCollection AddApiClientInfrastructure(this IServiceCollection services)
    {
        services.AddTransient<ApiAuthDelegatingHandler>();
        return services;
    }

    /// <summary>Registers a BBF.Api-backed implementation of a BBF.Shared client interface, with the Bearer token attached automatically.</summary>
    public static IServiceCollection AddApiHttpClient<TInterface, TImplementation>(
        this IServiceCollection services, string apiBaseUrl, TimeSpan? timeout = null)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        services.AddHttpClient<TInterface, TImplementation>(c =>
            {
                c.BaseAddress = new Uri(apiBaseUrl);
                if (timeout is { } t) c.Timeout = t;
            })
            .AddHttpMessageHandler<ApiAuthDelegatingHandler>();
        return services;
    }
}
