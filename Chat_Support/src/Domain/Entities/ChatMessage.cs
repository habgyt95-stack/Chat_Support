

namespace Chat_Support.Domain.Entities;

public class ChatMessage:BaseAuditableEntity
{
    public string Content { get; set; } = string.Empty;
    public int? SenderId { get; set; }
    public int ChatRoomId { get; set; }
    public MessageType Type { get; set; } = MessageType.Text;
    public string? AttachmentUrl { get; set; }
    public string? AttachmentType { get; set; }
    public bool IsEdited { get; set; }
    public DateTime? EditedAt { get; set; }
    public bool IsDeleted { get; set; }
    public int? ReplyToMessageId { get; set; }

    public virtual KciUser Sender { get; set; } = null!;
    public virtual ChatRoom ChatRoom { get; set; } = null!;
    public virtual ChatMessage? ReplyToMessage { get; set; }
    public virtual ICollection<ChatMessage> Replies { get; set; } = new List<ChatMessage>();
    public virtual ICollection<MessageReaction> Reactions { get; set; } = new List<MessageReaction>();
    public virtual ICollection<MessageStatus> Statuses { get; set; } = new List<MessageStatus>();
}
