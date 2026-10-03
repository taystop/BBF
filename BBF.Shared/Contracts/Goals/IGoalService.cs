namespace BBF.Shared.Contracts.Goals;

public interface IGoalService
{
    Task<List<GoalDto>> GetGoalsAsync(int groupId, CancellationToken ct = default);
    Task<GoalDto> CreateAsync(int groupId, CreateGoalRequest request, CancellationToken ct = default);
    Task UpdateAsync(int groupId, int goalId, UpdateGoalRequest request, CancellationToken ct = default);
    Task SetActiveAsync(int groupId, int goalId, bool isActive, CancellationToken ct = default);
    Task AddContributionAsync(int groupId, int goalId, AddGoalContributionRequest request, CancellationToken ct = default);
}
