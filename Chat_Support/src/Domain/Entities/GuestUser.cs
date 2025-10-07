namespace Chat_Support.Domain.Entities;

public class GuestUser : BaseAuditableEntity
{
    public string SessionId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public DateTime LastActivityAt { get; set; }
    public bool IsActive { get; set; }

    public virtual ICollection<ChatRoom> ChatRooms { get; set; } = new List<ChatRoom>();
    public virtual ICollection<SupportTicket> SupportTickets { get; set; } = new List<SupportTicket>();
}
