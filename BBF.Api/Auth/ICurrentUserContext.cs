namespace BBF.Api.Auth;

/// <summary>
/// Replaces BBF.Web's old stateful, in-circuit UserContextService: there is no server-held "active
/// group" here — every request carries its own user id (from the JWT) and its own requested group id
/// (route/query parameter), and this verifies membership fresh each time. This is the one place group
/// membership is checked, so every endpoint gets the same enforcement instead of relying on each
/// service remembering to filter correctly.
/// </summary>
public interface ICurrentUserContext
{
    string UserId { get; }

    /// <summary>Throws <see cref="ForbiddenAccessException"/> if the current user isn't a member of <paramref name="requestedGroupId"/>; otherwise returns it.</summary>
    Task<int> GetVerifiedGroupIdAsync(int requestedGroupId, CancellationToken ct = default);
}
