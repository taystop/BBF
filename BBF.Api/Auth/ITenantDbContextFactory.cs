using BBF.Data;

namespace BBF.Api.Auth;

/// <summary>
/// Every BBF.Api service that touches a GroupId-bearing entity should get its DbContext from here,
/// never straight from <see cref="Microsoft.EntityFrameworkCore.IDbContextFactory{TContext}"/> — this
/// is what actually wires the verified group id into <see cref="ApplicationDbContext.CurrentGroupId"/>
/// so the EF query filters in BBF.Data take effect.
/// </summary>
public interface ITenantDbContextFactory
{
    /// <summary>Verifies the caller is a member of <paramref name="requestedGroupId"/>, then returns a context scoped to it.</summary>
    Task<ApplicationDbContext> CreateForGroupAsync(int requestedGroupId, CancellationToken ct = default);

    /// <summary>No group filter applied — only for trusted cross-tenant admin operations.</summary>
    Task<ApplicationDbContext> CreateUnscopedAsync(CancellationToken ct = default);
}
