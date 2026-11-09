using BBF.Data;
using BBF.Data.Entities;

namespace BBF.Api.Services;

/// <summary>
/// Private accounts and paychecks: their money counts in every group total, but only their owner sees
/// the details. Services apply these rules before anything leaves the API.
/// </summary>
public static class Privacy
{
    public const string HiddenAccountName = "Private account";
    public const string HiddenPaycheckName = "Private paycheck";
    public const string HiddenBillName = "Private bill";

    /// <summary>True when the item is private to someone other than <paramref name="userId"/>.</summary>
    public static bool IsHiddenFrom(string? privateToUserId, string userId) =>
        privateToUserId is not null && privateToUserId != userId;

    public static bool IsHiddenFrom(PaySchedule schedule, string userId) =>
        schedule.IsPrivate && schedule.UserId != userId;

    /// <summary>
    /// Leaves out transactions another member keeps private: anything in their private account, and the
    /// deposit matched to their private paycheck.
    /// </summary>
    public static IQueryable<Transaction> VisibleTo(this IQueryable<Transaction> query, ApplicationDbContext db, string userId) =>
        query.Where(t => (t.Account == null || t.Account.PrivateToUserId == null || t.Account.PrivateToUserId == userId)
            && !db.Paychecks.Any(p => p.TransactionId == t.Id && p.PaySchedule.IsPrivate && p.PaySchedule.UserId != userId));
}
