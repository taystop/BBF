using System.Net.Http.Headers;

namespace BBF.ApiClients;

/// <summary>Attaches the current circuit's Bearer token to every outgoing call to BBF.Api. Registered on each typed HttpClient via AddHttpMessageHandler.</summary>
public class ApiAuthDelegatingHandler(IApiAuthTokenProvider tokenProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var token = await tokenProvider.GetTokenAsync(ct);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, ct);
    }
}
