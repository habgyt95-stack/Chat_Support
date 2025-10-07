

namespace Chat_Support.Domain.Entities;

public class ChatRoomMember : BaseAuditableEntity
{
    public int UserId { get; set; }
    public int ChatRoomId { get; set; }
    public ChatRole Role { get; set; } = ChatRole.Member;
    public DateTime JoinedAt { get; set; } = DateTime.Now;
    public DateTime? LastSeenAt { get; set; }
    public bool IsMuted { get; set; }
    public bool IsDeleted { get; set; }
    public int? LastReadMessageId { get; set; }

    public virtual KciUser User { get; set; } = null!;
    public virtual ChatRoom ChatRoom { get; set; } = null!;
}
