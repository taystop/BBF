using BBF.Data;
using Microsoft.EntityFrameworkCore;

namespace BBF.Api.Auth;

public class TenantDbContextFactory(IDbContextFactory<ApplicationDbContext> inner, ICurrentUserContext currentUser)
    : ITenantDbContextFactory
{
    public async Task<ApplicationDbContext> CreateForGroupAsync(int requestedGroupId, CancellationToken ct = default)
    {
        var verifiedGroupId = await currentUser.GetVerifiedGroupIdAsync(requestedGroupId, ct);
        var db = await inner.CreateDbContextAsync(ct);
        db.CurrentGroupId = verifiedGroupId;
        return db;
    }

    public Task<ApplicationDbContext> CreateUnscopedAsync(CancellationToken ct = default) =>
        inner.CreateDbContextAsync(ct);
}
