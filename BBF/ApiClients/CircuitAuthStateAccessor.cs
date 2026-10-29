using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.AspNetCore.Components.Authorization;

namespace BBF.ApiClients;

/// <summary>
/// Bridges the circuit's cascading authentication state (only safely readable from a component, via
/// its Task&lt;AuthenticationState&gt; cascading parameter) to plain DI services like
/// ApiAuthTokenProvider that need it but can't receive cascading parameters themselves.
///
/// This can't just be a Scoped DI service: ApiAuthTokenProvider is reached through
/// AddHttpMessageHandler&lt;ApiAuthDelegatingHandler&gt;(), and handlers registered that way are
/// constructed from IHttpClientFactory's own internal handler-pool scope, not the circuit's scope —
/// a documented .NET gotcha. A "Scoped" registration of this class resolves to a DIFFERENT instance
/// for MainLayout (circuit scope) than for ApiAuthTokenProvider (handler-pool scope), even within the
/// same circuit, so anything written to one instance is invisible to the other.
///
/// Instead, entries are keyed by SynchronizationContext.Current, which every Blazor Server circuit
/// installs its own instance of and which stays consistent across this gap (verified: the handler's
/// SendAsync runs synchronously, before any await, as part of the calling component's own call stack,
/// so it observes the same circuit's SynchronizationContext.Current as the component that invoked it —
/// regardless of which scope constructed the handler object itself). ConditionalWeakTable holds only a
/// weak reference to that key, so entries are collected once a circuit's SynchronizationContext is no
/// longer referenced anywhere, instead of leaking one TaskCompletionSource per circuit for the life of
/// the process.
/// </summary>
public static class CircuitAuthStateAccessor
{
    private static readonly ConditionalWeakTable<SynchronizationContext, TaskCompletionSource<AuthenticationState>> Entries = new();

    /// <summary>Called once per circuit (MainLayout.OnInitialized) with the cascading AuthenticationState Task.</summary>
    public static void SetState(Task<AuthenticationState> stateTask)
    {
        var tcs = GetOrCreateEntry();
        _ = stateTask.ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully) tcs.TrySetResult(t.Result);
            else if (t.Exception is not null) tcs.TrySetException(t.Exception.InnerExceptions);
            else if (t.IsCanceled) tcs.TrySetCanceled();
        }, TaskScheduler.Default);
    }

    /// <summary>
    /// Awaited from ApiAuthTokenProvider. Safe to call before SetState has run for this circuit —
    /// both sides resolve to the same entry via GetOrCreateEntry, so whichever runs first just creates
    /// it and the other completes it later.
    /// </summary>
    public static Task<AuthenticationState> StateTask => GetOrCreateEntry().Task;

    private static TaskCompletionSource<AuthenticationState> GetOrCreateEntry()
    {
        var syncContext = SynchronizationContext.Current
            ?? throw new InvalidOperationException("No SynchronizationContext — this must be called from within a Blazor Server circuit.");
        return Entries.GetValue(syncContext, _ => new TaskCompletionSource<AuthenticationState>(TaskCreationOptions.RunContinuationsAsynchronously));
    }
}
