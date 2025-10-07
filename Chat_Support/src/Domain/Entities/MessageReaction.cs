

namespace Chat_Support.Domain.Entities;

public class MessageReaction : BaseAuditableEntity
{
    public int MessageId { get; set; }
    public int UserId { get; set; }
    public string Emoji { get; set; } = string.Empty;

    public virtual ChatMessage Message { get; set; } = null!;
    public virtual KciUser User { get; set; } = null!;
}
