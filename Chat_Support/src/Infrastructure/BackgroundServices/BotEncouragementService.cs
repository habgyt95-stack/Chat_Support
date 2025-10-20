using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Application.Support.Services;
using Chat_Support.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Chat_Support.Infrastructure.BackgroundServices;

/// <summary>
/// سرویس پس‌زمینه برای ارسال پیام‌های تشویقی و نگهداری کاربران در حین انتظار
/// </summary>
public class BotEncouragementService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BotEncouragementService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(2); // هر 2 دقیقه یکبار بررسی

    public BotEncouragementService(
        IServiceProvider serviceProvider,
        ILogger<BotEncouragementService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Bot Encouragement Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
                await SendEncouragementMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Service is stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Bot Encouragement Service");
            }
        }

        _logger.LogInformation("Bot Encouragement Service stopped.");
    }

    private async Task SendEncouragementMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var botService = scope.ServiceProvider.GetRequiredService<IVirtualBotService>();

        // پیدا کردن تیکت‌هایی که به ربات اختصاص دارند
        var botTickets = await context.SupportTickets
            .Include(t => t.AssignedAgent)
            .Include(t => t.ChatRoom)
            .Where(t => t.AssignedAgent != null && 
                       t.AssignedAgent.IsVirtualBot &&
                       (t.Status == SupportTicketStatus.Open || t.Status == SupportTicketStatus.InProgress))
            .ToListAsync(cancellationToken);

        foreach (var ticket in botTickets)
        {
            try
            {
                await botService.SendPeriodicMessagesAsync(ticket.ChatRoomId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending encouragement message for ticket {TicketId}", ticket.Id);
            }
        }

        if (botTickets.Any())
        {
            _logger.LogInformation("Sent encouragement check for {Count} active bot tickets", botTickets.Count);
        }
    }
}
