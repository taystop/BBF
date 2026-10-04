using System.Net.Http.Json;
using BBF.Shared.Contracts.Integrations;

namespace BBF.ApiClients;

public class HttpIntegrationsStatusService(HttpClient http) : IIntegrationsStatusService
{
    public async Task<OllamaStatusDto> GetOllamaStatusAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<OllamaStatusDto>("/api/integrations/ollama/status", ct)
            ?? new OllamaStatusDto(false, []);
}
