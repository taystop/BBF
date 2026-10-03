namespace BBF.Shared.Contracts.Groups;

public record GroupMemberDto(int Id, string UserId, string UserName, string Role, DateTime JoinedAt);

public record GroupDetailDto(int Id, string Name, DateTime CreatedAt, List<GroupMemberDto> Members);

public record CreateGroupRequest(string Name);

public record UpdateGroupRequest(string Name);

public record AddGroupMemberRequest(string Email);

public record ChangeGroupMemberRoleRequest(string Role);
