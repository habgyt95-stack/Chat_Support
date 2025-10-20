using Chat_Support.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Chat_Support.Infrastructure.BackgroundServices;

/// <summary>
/// سرویس پس‌زمینه برای انتقال خودکار تیکت‌های ربات به agent های واقعی
/// </summary>
public class BotTicketReassignmentService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BotTicketReassignmentService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30); // هر 30 ثانیه

    public BotTicketReassignmentService(
        IServiceProvider serviceProvider,
        ILogger<BotTicketReassignmentService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Bot Ticket Reassignment Service started.");

        // تاخیر اولیه برای اطمینان از آماده بودن سیستم
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndReassignTicketsAsync(stoppingToken);
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Bot Ticket Reassignment Service");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        _logger.LogInformation("Bot Ticket Reassignment Service stopped.");
    }

    private async Task CheckAndReassignTicketsAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"🔍 [{DateTime.Now:HH:mm:ss}] BotTicketReassignment: Starting check cycle...");
        
        using var scope = _serviceProvider.CreateScope();
        var agentAssignment = scope.ServiceProvider.GetRequiredService<IAgentAssignmentService>();

        try
        {
            Console.WriteLine($"🔄 [{DateTime.Now:HH:mm:ss}] BotTicketReassignment: Calling ReassignBotTicketsToAvailableAgentsAsync...");
            await agentAssignment.ReassignBotTicketsToAvailableAgentsAsync(cancellationToken);
            Console.WriteLine($"✅ [{DateTime.Now:HH:mm:ss}] BotTicketReassignment: Check cycle completed successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [{DateTime.Now:HH:mm:ss}] BotTicketReassignment ERROR: {ex.Message}");
            _logger.LogError(ex, "Error reassigning bot tickets to available agents");
        }
    }
}
