namespace BBF.Shared.Contracts.Auth;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
}

public interface IGroupService
{
    /// <summary>Lists the signed-in user's groups, auto-creating a personal group on first call if they have none.</summary>
    Task<List<GroupDto>> GetMyGroupsAsync(CancellationToken ct = default);
}
