using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Domain.Entities;
using Chat_Support.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Chat_Support.Infrastructure.Service;

/// <summary>
/// مدیریت هوشمند وضعیت پشتیبانان با قابلیت تشخیص خودکار و تنظیم دستی با TTL
/// </summary>
public class AgentStatusManager : IAgentStatusManager
{
    private readonly IApplicationDbContext _context;
    
    // تنظیمات TTL: وضعیت دستی برای 4 ساعت معتبر است
    private static readonly TimeSpan ManualStatusTTL = TimeSpan.FromMinutes(15);
    
    // آستانه‌های تشخیص خودکار - تنظیمات محافظه‌کارانه‌تر
    private static readonly TimeSpan AvailableThreshold = TimeSpan.FromMinutes(10);  // فعالیت کمتر از 10 دقیقه پیش
    private static readonly TimeSpan AwayThreshold = TimeSpan.FromMinutes(30);      // فعالیت بین 10-30 دقیقه پیش
    // بیش از 30 دقیقه بدون فعالیت => Offline

    public AgentStatusManager(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// تنظیم دستی وضعیت توسط پشتیبان با TTL 15 دقیقه‌ای
    /// </summary>
    public async Task SetManualStatusAsync(int agentId, AgentStatus status, CancellationToken cancellationToken = default)
    {
        var agent = await _context.SupportAgents
            .FirstOrDefaultAsync(a => a.UserId == agentId, cancellationToken);

        if (agent == null)
        {
            Console.WriteLine($"⚠️ Agent not found: UserId={agentId}");
            return;
        }

        var now = DateTime.UtcNow;
        
        Console.WriteLine($"📝 Setting manual status: AgentId={agent.Id}, UserId={agentId}, OldStatus={agent.AgentStatus}, NewStatus={status}");
        
        // ذخیره وضعیت دستی با زمان انقضا
        agent.AgentStatus = status;
        agent.ManualStatusSetAt = now;
        agent.ManualStatusExpiry = now.Add(ManualStatusTTL);
        agent.LastActivityAt = now;

        var changeCount = await _context.SaveChangesAsync(cancellationToken);
        
        Console.WriteLine($"✅ Manual status saved: AgentId={agent.Id}, NewStatus={agent.AgentStatus}, ExpiresAt={agent.ManualStatusExpiry}, ChangeCount={changeCount}");
    }

    /// <summary>
    /// تشخیص خودکار وضعیت بر اساس فعالیت و بار کاری
    /// </summary>
    public async Task<AgentStatus> DetectAutomaticStatusAsync(int agentId, CancellationToken cancellationToken = default)
    {
        var agent = await _context.SupportAgents
            .FirstOrDefaultAsync(a => a.UserId == agentId, cancellationToken);

        if (agent == null || !agent.IsActive)
            return AgentStatus.Offline;

        var now = DateTime.UtcNow;
        var lastActivity = agent.LastActivityAt ?? now.AddHours(-1);
        var timeSinceActivity = now - lastActivity;

        AgentStatus detectedStatus;

        // تشخیص بر اساس زمان آخرین فعالیت
        if (timeSinceActivity <= AvailableThreshold)
        {
            // فعال در 5 دقیقه اخیر
            if (agent.CurrentActiveChats >= agent.MaxConcurrentChats)
                detectedStatus = AgentStatus.Busy;
            else
                detectedStatus = AgentStatus.Available;
        }
        else if (timeSinceActivity <= AwayThreshold)
        {
            // بین 5 تا 15 دقیقه بدون فعالیت
            detectedStatus = AgentStatus.Away;
        }
        else
        {
            // بیش از 15 دقیقه بدون فعالیت
            detectedStatus = AgentStatus.Offline;
        }

        // ذخیره وضعیت تشخیص داده شده
        agent.AutoDetectedStatus = detectedStatus;
        await _context.SaveChangesAsync(cancellationToken);

        return detectedStatus;
    }

    /// <summary>
    /// دریافت وضعیت فعلی (دستی یا خودکار)
    /// اگر وضعیت دستی منقضی شده باشد، به خودکار برمی‌گردد
    /// </summary>
    public async Task<AgentStatus> GetEffectiveStatusAsync(int agentId, CancellationToken cancellationToken = default)
    {
        var agent = await _context.SupportAgents
            .FirstOrDefaultAsync(a => a.UserId == agentId, cancellationToken);

        if (agent == null || !agent.IsActive)
            return AgentStatus.Offline;

        var now = DateTime.UtcNow;

        // بررسی اعتبار وضعیت دستی
        if (agent.ManualStatusExpiry.HasValue && agent.ManualStatusExpiry.Value > now)
        {
            // وضعیت دستی هنوز معتبر است
            return agent.AgentStatus ?? AgentStatus.Offline;
        }

        // وضعیت دستی منقضی شده یا وجود ندارد - استفاده از تشخیص خودکار
        var autoStatus = await DetectAutomaticStatusAsync(agentId, cancellationToken);
        
        // به‌روزرسانی وضعیت در دیتابیس
        agent.AgentStatus = autoStatus;
        agent.ManualStatusSetAt = null;
        agent.ManualStatusExpiry = null;
        
        await _context.SaveChangesAsync(cancellationToken);

        return autoStatus;
    }

    /// <summary>
    /// به‌روزرسانی آخرین فعالیت پشتیبان
    /// این متد در هر فعالیت پشتیبان (ارسال پیام، خواندن، ...) فراخوانی می‌شود
    /// </summary>
    public async Task UpdateActivityAsync(int agentId, CancellationToken cancellationToken = default)
    {
        var agent = await _context.SupportAgents
            .FirstOrDefaultAsync(a => a.UserId == agentId, cancellationToken);

        if (agent == null)
            return;

        agent.LastActivityAt = DateTime.UtcNow;
        
        // اگر وضعیت دستی ندارد، وضعیت خودکار را به‌روزرسانی کن
        if (!agent.ManualStatusExpiry.HasValue || agent.ManualStatusExpiry.Value <= DateTime.UtcNow)
        {
            await DetectAutomaticStatusAsync(agentId, cancellationToken);
        }
        
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// بررسی و به‌روزرسانی همه پشتیبانان که وضعیت دستی‌شان منقضی شده
    /// این متد توسط Background Service فراخوانی می‌شود
    /// </summary>
    public async Task UpdateExpiredManualStatusesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var expiredAgents = await _context.SupportAgents
            .Where(a => a.IsActive 
                && a.ManualStatusExpiry.HasValue 
                && a.ManualStatusExpiry.Value <= now)
            .ToListAsync(cancellationToken);

        foreach (var agent in expiredAgents)
        {
            // بازگشت به تشخیص خودکار
            var autoStatus = await DetectAutomaticStatusAsync(agent.UserId, cancellationToken);
            
            agent.AgentStatus = autoStatus;
            agent.ManualStatusSetAt = null;
            agent.ManualStatusExpiry = null;
        }

        if (expiredAgents.Any())
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// به‌روزرسانی خودکار وضعیت همه پشتیبانان فعال
    /// برای استفاده در Background Service
    /// </summary>
    public async Task UpdateAllAgentStatusesAsync(CancellationToken cancellationToken = default)
    {
        // ابتدا وضعیت‌های دستی منقضی شده را به‌روزرسانی کن
        await UpdateExpiredManualStatusesAsync(cancellationToken);

        // سپس وضعیت خودکار پشتیبانانی که وضعیت دستی ندارند را به‌روزرسانی کن
        var autoManagedAgents = await _context.SupportAgents
            .Where(a => a.IsActive 
                && (!a.ManualStatusExpiry.HasValue || a.ManualStatusExpiry.Value <= DateTime.UtcNow))
            .ToListAsync(cancellationToken);

        foreach (var agent in autoManagedAgents)
        {
            await DetectAutomaticStatusAsync(agent.UserId, cancellationToken);
        }
    }

    /// <summary>
    /// دریافت اطلاعات کامل وضعیت پشتیبان (برای نمایش در UI)
    /// </summary>
    public async Task<AgentStatusInfo> GetStatusInfoAsync(int agentId, CancellationToken cancellationToken = default)
    {
        var agent = await _context.SupportAgents
            .FirstOrDefaultAsync(a => a.UserId == agentId, cancellationToken);

        if (agent == null)
        {
            return new AgentStatusInfo
            {
                CurrentStatus = AgentStatus.Offline,
                IsManuallySet = false,
                ExpiresAt = null,
                TimeRemaining = null,
                AutoDetectedStatus = AgentStatus.Offline
            };
        }

        var now = DateTime.UtcNow;
        var isManual = agent.ManualStatusExpiry.HasValue && agent.ManualStatusExpiry.Value > now;

        return new Application.Common.Interfaces.AgentStatusInfo
        {
            CurrentStatus = agent.AgentStatus ?? AgentStatus.Offline,
            IsManuallySet = isManual,
            ExpiresAt = isManual ? agent.ManualStatusExpiry : null,
            TimeRemaining = isManual ? agent.ManualStatusExpiry!.Value - now : null,
            AutoDetectedStatus = agent.AutoDetectedStatus ?? AgentStatus.Offline,
            LastActivityAt = agent.LastActivityAt
        };
    }
}
