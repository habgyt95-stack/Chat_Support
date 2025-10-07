

namespace Chat_Support.Domain.Entities;

public class MessageStatus:BaseAuditableEntity
{
    public int MessageId { get; set; }
    public int? UserId { get; set; }
    public ReadStatus Status { get; set; }
    public DateTime StatusAt { get; set; }

    public virtual ChatMessage Message { get; set; } = null!;
    public virtual KciUser User { get; set; } = null!;
}
