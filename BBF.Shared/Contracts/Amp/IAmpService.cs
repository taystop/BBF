namespace BBF.Shared.Contracts.Amp;

public interface IAmpService
{
    Task<List<AmpInstance>> GetInstancesAsync(CancellationToken ct = default);
    Task<AmpInstanceStatus?> GetInstanceStatusAsync(string instanceId, CancellationToken ct = default);
    Task<bool> StartInstanceAsync(string instanceId, CancellationToken ct = default);
    Task<bool> StopInstanceAsync(string instanceId, CancellationToken ct = default);
    Task<bool> RestartInstanceAsync(string instanceId, CancellationToken ct = default);
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}
