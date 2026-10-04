namespace BBF.Shared.Contracts.Users;

public record UserSummaryDto(string Id, string? Email, bool EmailConfirmed, bool IsLockedOut, List<string> Roles);

public record CreateUserRequest(string Email, string Password, List<string> Roles);

public record UpdateUserRequest(string Email, string? NewPassword, List<string> Roles);
