using System;
using System.Collections.Generic;
using Chat_Support.Domain.Enums;

namespace Chat_Support.Domain.Entities;

public class SupportGuestChatRoomMember : BaseAuditableEntity
{
    public int GuestUserId { get; set; }
    public int ChatRoomId { get; set; }
    public ChatRole Role { get; set; } = ChatRole.Member;
    public DateTime JoinedAt { get; set; } = DateTime.Now;
    public DateTime? LastSeenAt { get; set; }
    public bool IsMuted { get; set; }
    public bool IsDeleted { get; set; }
    public int? LastReadMessageId { get; set; }

    public virtual GuestUser GuestUser { get; set; } = null!;
    public virtual ChatRoom ChatRoom { get; set; } = null!;
}
