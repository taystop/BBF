namespace BBF.Shared.Contracts.Auth;

public record LoginRequest(string UserName, string Password);

/// <summary>Body for BBF.Web's cookie-session-to-JWT exchange — see AuthEndpoints.MapAuthEndpoints in BBF.Api. Requires the X-Internal-Key header; never called by BBF.Mobile.</summary>
public record InternalTokenRequest(string UserId);

public record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, string UserId, string UserName);

public record GroupDto(int Id, string Name);
