using Chat_Support.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Chat_Support.Infrastructure.Service;

/// <summary>
/// سرویس پس‌زمینه برای نظارت و به‌روزرسانی خودکار وضعیت پشتیبانان
/// هر 2 دقیقه یکبار اجرا می‌شود
/// </summary>
public class AgentStatusMonitorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AgentStatusMonitorService> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(2);

    public AgentStatusMonitorService(
        IServiceProvider serviceProvider,
        ILogger<AgentStatusMonitorService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🔄 Agent Status Monitor Service started");

        // تاخیر اولیه 30 ثانیه برای اطمینان از راه‌اندازی کامل سیستم
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await MonitorAndUpdateAgentStatusesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in Agent Status Monitor Service");
            }

            // منتظر بمان تا چک بعدی
            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // سرویس در حال متوقف شدن است
                break;
            }
        }

        _logger.LogInformation("🛑 Agent Status Monitor Service stopped");
    }

    private async Task MonitorAndUpdateAgentStatusesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var statusManager = scope.ServiceProvider.GetRequiredService<IAgentStatusManager>();

        _logger.LogDebug("🔍 Checking agent statuses...");

        try
        {
            // به‌روزرسانی همه وضعیت‌ها
            await statusManager.UpdateAllAgentStatusesAsync(cancellationToken);
            
            _logger.LogDebug("✅ Agent statuses updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to update agent statuses");
        }
    }
}
