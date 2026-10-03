namespace BBF.Api.Auth;

/// <summary>Thrown when the authenticated user isn't a member of the group they requested. Mapped to 403 by the exception handler in Program.cs.</summary>
public class ForbiddenAccessException(string message) : Exception(message);
