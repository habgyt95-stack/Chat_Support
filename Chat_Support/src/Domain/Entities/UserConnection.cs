

namespace Chat_Support.Domain.Entities;

public class UserConnection:BaseAuditableEntity
{
    public int? UserId { get; set; }
    public string ConnectionId { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; }
    public bool IsActive { get; set; }

    public virtual KciUser User { get; set; } = null!;
}
