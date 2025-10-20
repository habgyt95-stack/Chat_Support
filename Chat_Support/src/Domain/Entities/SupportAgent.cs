namespace Chat_Support.Domain.Entities;

public class SupportAgent : BaseAuditableEntity
{
    public int UserId { get; set; }
    public bool IsActive { get; set; }
    public AgentStatus? AgentStatus { get; set; }
    public int CurrentActiveChats { get; set; }
    public int MaxConcurrentChats { get; set; }
    public DateTime? LastActivityAt { get; set; }
    
    /// <summary>
    /// آیا این agent یک ربات مجازی است که همیشه آنلاین است؟
    /// </summary>
    public bool IsVirtualBot { get; set; } = false;

    // فیلدهای مدیریت وضعیت دستی و خودکار
    /// <summary>
    /// زمانی که پشتیبان آخرین بار وضعیت خود را به صورت دستی تنظیم کرده
    /// </summary>
    public DateTime? ManualStatusSetAt { get; set; }
    
    /// <summary>
    /// زمان انقضای وضعیت دستی (پیشفرض: 4 ساعت بعد از تنظیم دستی)
    /// بعد از این زمان، سیستم به تشخیص خودکار برمی‌گردد
    /// </summary>
    public DateTime? ManualStatusExpiry { get; set; }
    
    /// <summary>
    /// وضعیت تشخیص داده شده توسط سیستم بر اساس فعالیت
    /// </summary>
    public AgentStatus? AutoDetectedStatus { get; set; }

    public virtual KciUser? User { get; set; }
    public virtual ICollection<SupportTicket>? AssignedTickets { get; set; }
}
