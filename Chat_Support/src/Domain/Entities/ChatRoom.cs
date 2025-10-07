

namespace Chat_Support.Domain.Entities;

public class ChatRoom : BaseAuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsGroup { get; set; }
    public string? Avatar { get; set; }
    public int? RegionId { get; set; }
    public int? CreatedById { get; set; }
    public string? GuestIdentifier { get; set; }
    public bool IsDeleted { get; set; }
    public ChatRoomType ChatRoomType { get; set; } = ChatRoomType.UserToUser;
    
    public virtual Region? Region { get; set; }
    public new virtual KciUser? CreatedBy { get; set; }
    public virtual ICollection<ChatRoomMember> Members { get; set; } = new List<ChatRoomMember>();
    public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
