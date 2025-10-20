using Chat_Support.Domain.Enums;

namespace Chat_Support.Application.Common.Interfaces;

public interface IAgentStatusManager
{
    /// <summary>
    /// تنظیم دستی وضعیت توسط پشتیبان
    /// </summary>
    Task SetManualStatusAsync(int agentId, AgentStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// تشخیص خودکار وضعیت بر اساس فعالیت
    /// </summary>
    Task<AgentStatus> DetectAutomaticStatusAsync(int agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// دریافت وضعیت موثر (دستی یا خودکار)
    /// </summary>
    Task<AgentStatus> GetEffectiveStatusAsync(int agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// به‌روزرسانی آخرین فعالیت
    /// </summary>
    Task UpdateActivityAsync(int agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// به‌روزرسانی وضعیت‌های دستی منقضی شده
    /// </summary>
    Task UpdateExpiredManualStatusesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// به‌روزرسانی وضعیت همه پشتیبانان
    /// </summary>
    Task UpdateAllAgentStatusesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// دریافت اطلاعات کامل وضعیت
    /// </summary>
    Task<AgentStatusInfo> GetStatusInfoAsync(int agentId, CancellationToken cancellationToken = default);
}

public class AgentStatusInfo
{
    public AgentStatus CurrentStatus { get; set; }
    public bool IsManuallySet { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public TimeSpan? TimeRemaining { get; set; }
    public AgentStatus AutoDetectedStatus { get; set; }
    public DateTime? LastActivityAt { get; set; }
}
