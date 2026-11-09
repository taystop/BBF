namespace BBF.Mobile.Push;

/// <summary>
/// Hands the page a tapped notification points to (its "link" data, e.g. "/money") from Android to
/// the Blazor UI. The tap can arrive before Blazor has started (cold start), so the link is kept until
/// MainLayout takes it; when the app is already open, the event lets it navigate right away.
/// </summary>
public static class NotificationNavigator
{
    private static readonly object Gate = new();
    private static (string Link, int? GroupId)? _pending;

    public static event Action? LinkRequested;

    /// <summary>Called by the platform with the tapped notification's data. Ignores anything that isn't an app page.</summary>
    public static void Request(string? link, string? groupId)
    {
        // Only in-app paths: "/money" yes, "//evil.example" or "https://..." no.
        if (string.IsNullOrEmpty(link) || !link.StartsWith('/') || link.StartsWith("//")) return;

        lock (Gate)
            _pending = (link, int.TryParse(groupId, out var id) ? id : null);
        LinkRequested?.Invoke();
    }

    /// <summary>The waiting link, if any, cleared so it's only followed once.</summary>
    public static (string Link, int? GroupId)? Take()
    {
        lock (Gate)
        {
            var pending = _pending;
            _pending = null;
            return pending;
        }
    }
}
