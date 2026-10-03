namespace BBF.Shared.Contracts.Goals;

public record GoalContributionDto(int Id, decimal Amount, DateTime ContributedAt, int? TransactionId);

public record GoalDto(
    int Id,
    string Name,
    int? AccountId,
    string? AccountName,
    decimal TargetAmount,
    DateTime? TargetDate,
    bool IsActive,
    decimal ProgressAmount,
    List<GoalContributionDto> Contributions);

public record CreateGoalRequest(string Name, decimal TargetAmount, DateTime? TargetDate, int? AccountId);

public record UpdateGoalRequest(string Name, decimal TargetAmount, DateTime? TargetDate, int? AccountId);

public record AddGoalContributionRequest(decimal Amount, DateTime ContributedAt, int? TransactionId);
