namespace BBF.Shared.Contracts.HomeAssistant;

/// <summary>Household-wide, no tenancy — one physical smart home shared by everyone with access to the app.</summary>
public interface IHomeAssistantService
{
    Task<List<HaEntity>> GetEntitiesAsync(CancellationToken ct = default);
    Task<List<HaEntity>> GetEntitiesByDomainAsync(string domain, CancellationToken ct = default);
    Task<HaEntity?> GetEntityAsync(string entityId, CancellationToken ct = default);
    Task<bool> CallServiceAsync(HaServiceCallRequest request, CancellationToken ct = default);
    Task<bool> TurnOnAsync(string entityId, CancellationToken ct = default);
    Task<bool> TurnOffAsync(string entityId, CancellationToken ct = default);
    Task<bool> ToggleAsync(string entityId, CancellationToken ct = default);
    Task<bool> SetTemperatureAsync(string entityId, double temperature, CancellationToken ct = default);
    Task<bool> SetHvacModeAsync(string entityId, string mode, CancellationToken ct = default);
    Task<bool> SetFanSpeedAsync(string entityId, string percentage, CancellationToken ct = default);
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}
