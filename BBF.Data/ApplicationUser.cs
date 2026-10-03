using BBF.Data.Entities;
using Microsoft.AspNetCore.Identity;

namespace BBF.Data
{
    public class ApplicationUser : IdentityUser
    {
        public ICollection<UserGroupMember> GroupMemberships { get; set; } = [];
        public ICollection<ChatConversation> ChatConversations { get; set; } = [];
    }
}
