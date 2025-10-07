

namespace Chat_Support.Domain.Entities;

public class SupportTicket : BaseAuditableEntity
{
    public int? RequesterUserId { get; set; }
    public virtual KciUser? RequesterUser { get; set; }

    public int? RequesterGuestId { get; set; }
    public virtual GuestUser? RequesterGuest { get; set; }

    public int? AssignedAgentUserId { get; set; }
    public virtual SupportAgent? AssignedAgent { get; set; }

    public int? RegionId { get; set; }
    public virtual Region? Region { get; set; }

    public int ChatRoomId { get; set; }
    public virtual ChatRoom ChatRoom { get; set; } = null!;

    public SupportTicketStatus Status { get; set; }

    public DateTime? ClosedAt { get; set; }
}
