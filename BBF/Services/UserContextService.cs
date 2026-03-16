using BBF.Data;
using BBF.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BBF.Services;

public class UserContextService
{
    private readonly ApplicationDbContext _db;

    public UserContextService(ApplicationDbContext db)
    {
        _db = db;
    }

    public string? UserId { get; private set; }
    public int? ActiveGroupId { get; private set; }
    public List<UserGroup> Groups { get; private set; } = [];
    public bool IsInitialized { get; private set; }

    public event Action? OnGroupChanged;

    public async Task InitializeAsync(string userId)
    {
        if (IsInitialized && UserId == userId) return;

        UserId = userId;

        // Load user's groups
        Groups = await _db.UserGroupMembers
            .Where(m => m.UserId == userId)
            .Include(m => m.Group)
            .Select(m => m.Group)
            .OrderBy(g => g.Name)
            .ToListAsync();

        // Auto-create "Personal" group if user has no groups
        if (Groups.Count == 0)
        {
            var user = await _db.Users.FindAsync(userId);
            var displayName = user?.UserName ?? "User";

            var personalGroup = new UserGroup
            {
                Name = $"{displayName}'s Budget",
                CreatedAt = DateTime.UtcNow
            };
            _db.UserGroups.Add(personalGroup);
            await _db.SaveChangesAsync();

            _db.UserGroupMembers.Add(new UserGroupMember
            {
                GroupId = personalGroup.Id,
                UserId = userId,
                Role = "Owner",
                JoinedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            Groups = [personalGroup];
        }

        // Set active group to first group
        ActiveGroupId = Groups.FirstOrDefault()?.Id;
        IsInitialized = true;
    }

    public void SetActiveGroup(int groupId)
    {
        if (Groups.Any(g => g.Id == groupId))
        {
            ActiveGroupId = groupId;
            OnGroupChanged?.Invoke();
        }
    }

    public async Task RefreshGroupsAsync()
    {
        if (UserId is null) return;

        Groups = await _db.UserGroupMembers
            .Where(m => m.UserId == UserId)
            .Include(m => m.Group)
            .Select(m => m.Group)
            .OrderBy(g => g.Name)
            .ToListAsync();

        // If active group was removed, switch to first available
        if (ActiveGroupId.HasValue && !Groups.Any(g => g.Id == ActiveGroupId))
        {
            ActiveGroupId = Groups.FirstOrDefault()?.Id;
            OnGroupChanged?.Invoke();
        }
    }
}
