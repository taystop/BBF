using BBF.Shared.Contracts.Auth;

namespace BBF.Shared;

/// <summary>
/// Replacement for the old UserContextService's group-tracking half, for pages that have moved onto
/// the BBF.Api-backed interfaces. Scoped (one per circuit) — same lifetime UserContextService had.
/// Deliberately thinner than the original: no recurring-transaction materialization side effect (that's
/// a real BackgroundService in BBF.Api now — see Phase 1b) and no DB access of its own, since tenancy
/// enforcement now happens server-side regardless of what this caches client-side for the UI.
/// </summary>
public class ApiGroupContext(IGroupService groupService)
{
    public List<GroupDto> Groups { get; private set; } = [];
    public int? ActiveGroupId { get; private set; }
    public bool IsInitialized { get; private set; }

    public event Action? OnGroupChanged;

    public async Task InitializeAsync()
    {
        if (IsInitialized) return;

        Groups = await groupService.GetMyGroupsAsync();
        ActiveGroupId = Groups.FirstOrDefault()?.Id;
        IsInitialized = true;

        // Pages render before MainLayout finishes this call (it runs in OnAfterRenderAsync, after the
        // rest of the tree's first render), so any page that read ActiveGroupId during its own
        // OnInitializedAsync saw it as null. Without this, those pages never learn the group arrived —
        // SetActiveGroup/RefreshGroupsAsync below already raise this same event for the same reason.
        OnGroupChanged?.Invoke();
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
        Groups = await groupService.GetMyGroupsAsync();

        if (ActiveGroupId.HasValue && !Groups.Any(g => g.Id == ActiveGroupId))
        {
            ActiveGroupId = Groups.FirstOrDefault()?.Id;
            OnGroupChanged?.Invoke();
        }
    }
}
