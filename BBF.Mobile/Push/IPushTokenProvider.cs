namespace BBF.Mobile.Push;

/// <summary>Platform-specific access to the current FCM registration token.</summary>
public interface IPushTokenProvider
{
    Task<string?> GetCurrentTokenAsync();
}
