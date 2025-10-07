

namespace Chat_Support.Domain.Entities;

public partial class TicketReply : BaseAuditableEntity
{

    public int TicketId { get; set; }

    public int UserId { get; set; }

    public string Message { get; set; } = null!;

    public virtual SupportTicket Ticket { get; set; } = null!;

    public virtual KciUser User { get; set; } = null!;
}
