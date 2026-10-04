namespace BBF.Shared.Contracts.Integrations;

public interface IIntegrationsStatusService
{
    Task<OllamaStatusDto> GetOllamaStatusAsync(CancellationToken ct = default);
}
