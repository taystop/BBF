namespace BBF.Api.Notifications;

public record NotificationMessage(string Title, string Body, IReadOnlyDictionary<string, string>? Data = null);
