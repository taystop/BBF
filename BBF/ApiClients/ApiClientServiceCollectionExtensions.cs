namespace BBF.ApiClients;

public static class ApiClientServiceCollectionExtensions
{
    /// <summary>
    /// Registers the shared plumbing every BBF.Api-backed client needs: the unauthenticated client used
    /// to fetch the internal token itself, plus the token provider and auth handler the typed clients
    /// below attach to.
    /// </summary>
    public static IServiceCollection AddApiClientInfrastructure(this IServiceCollection services, string apiBaseUrl)
    {
        services.AddHttpClient("BBF.Api.Internal", c => c.BaseAddress = new Uri(apiBaseUrl));
        services.AddScoped<IApiAuthTokenProvider, ApiAuthTokenProvider>();
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
