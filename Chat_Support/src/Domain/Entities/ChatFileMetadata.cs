

namespace Chat_Support.Domain.Entities;

public class ChatFileMetadata : BaseAuditableEntity
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public int ChatRoomId { get; set; }
    public int? UploadedById { get; set; } 
    public DateTime UploadedDate { get; set; }
    public MessageType MessageType { get; set; }

    public virtual ChatRoom ChatRoom { get; set; } = null!;
    public virtual KciUser UploadedBy { get; set; } = null!;
}
