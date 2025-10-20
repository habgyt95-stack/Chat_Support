using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Domain.Entities;
using Chat_Support.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Chat_Support.Application.Support.Services;

public interface IVirtualBotService
{
    Task<SupportAgent> EnsureVirtualBotExistsAsync(CancellationToken cancellationToken = default);
    Task SendPeriodicMessagesAsync(int chatRoomId, CancellationToken cancellationToken = default);
}

public class VirtualBotService : IVirtualBotService
{
    private readonly IApplicationDbContext _context;
    private readonly IChatHubService _chatHubService;
    private readonly ILogger<VirtualBotService> _logger;
    
    // پیام‌های متنوع برای ارسال به کاربر در حین انتظار
    private readonly List<string> _waitingMessages = new()
    {
        "ممنون از صبر شما! یکی از پشتیبانان ما به زودی به شما پاسخ خواهند داد. 😊",
        "در حال بررسی درخواست شما هستیم... لطفاً کمی صبر کنید. ⏳",
        "پشتیبانان ما در حال رسیدگی به سایر کاربران هستند. شما در اولویت هستید! 🙏",
        "به زودی یکی از همکاران ما با شما در ارتباط خواهد بود. با تشکر از شکیبایی شما. 💚"
    };

    public VirtualBotService(
        IApplicationDbContext context, 
        IChatHubService chatHubService,
        ILogger<VirtualBotService> logger)
    {
        _context = context;
        _chatHubService = chatHubService;
        _logger = logger;
    }

    public async Task<SupportAgent> EnsureVirtualBotExistsAsync(CancellationToken cancellationToken = default)
    {
        var existingBot = await _context.SupportAgents
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.IsVirtualBot, cancellationToken);

        if (existingBot != null)
        {
            return existingBot;
        }

        _logger.LogInformation("Creating virtual support bot...");

        // ایجاد کاربر برای ربات
        var botUser = new KciUser
        {
            UserName = "support-bot",
            FirstName = "دستیار",
            LastName = "پشتیبانی",
            Tel = "0000000000",
            Email = "bot@support.local",
            Description = "ربات مجازی پشتیبانی خودکار",
            Password = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()), // رمز تصادفی
            Enable = true,
            RegionId = null // دسترسی به همه مناطق
        };

        _context.KciUsers.Add(botUser);
        await _context.SaveChangesAsync(cancellationToken);

        // ایجاد agent ربات
        var botAgent = new SupportAgent
        {
            UserId = botUser.Id,
            IsActive = true,
            IsVirtualBot = true,
            AgentStatus = AgentStatus.Available,
            CurrentActiveChats = 0,
            MaxConcurrentChats = 9999, // ظرفیت نامحدود
            LastActivityAt = DateTime.Now
        };

        _context.SupportAgents.Add(botAgent);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Virtual bot created successfully with UserId: {UserId}", botUser.Id);

        return botAgent;
    }

    public async Task SendPeriodicMessagesAsync(int chatRoomId, CancellationToken cancellationToken = default)
    {
        // این متد می‌تواند در background job صدا زده شود
        // برای مثال با استفاده از Hangfire یا Quartz
        // یا می‌توانید در SignalR Hub آن را با timer صدا بزنید
        
        var ticket = await _context.SupportTickets
            .Include(t => t.AssignedAgent)
            .FirstOrDefaultAsync(t => t.ChatRoomId == chatRoomId && 
                                      t.AssignedAgent != null && 
                                      t.AssignedAgent.IsVirtualBot, 
                                      cancellationToken);

        if (ticket == null)
        {
            return; // تیکت پیدا نشد یا دیگر به ربات اختصاص ندارد
        }

        // بررسی آخرین پیام ربات
        var lastBotMessage = await _context.ChatMessages
            .Where(m => m.ChatRoomId == chatRoomId && m.SenderId == ticket.AssignedAgent!.UserId)
            .OrderByDescending(m => m.Created)
            .FirstOrDefaultAsync(cancellationToken);

        // اگر از آخرین پیام بیش از 2 دقیقه گذشته، پیام جدید بفرست
        if (lastBotMessage == null || (DateTime.Now - lastBotMessage.Created.DateTime).TotalMinutes >= 2)
        {
            var randomMessage = _waitingMessages[Random.Shared.Next(_waitingMessages.Count)];
            
            var encouragementMessage = new ChatMessage
            {
                Content = randomMessage,
                SenderId = ticket.AssignedAgent!.UserId,
                ChatRoomId = chatRoomId,
                Type = MessageType.Text
            };

            _context.ChatMessages.Add(encouragementMessage);
            await _context.SaveChangesAsync(cancellationToken);

            // ارسال پیام به SignalR
            try
            {
                var messageDto = new Application.Chats.DTOs.ChatMessageDto
                {
                    Id = encouragementMessage.Id,
                    Content = encouragementMessage.Content,
                    SenderId = encouragementMessage.SenderId.ToString()!,
                    SenderFullName = "دستیار پشتیبانی",
                    ChatRoomId = chatRoomId,
                    Type = MessageType.Text,
                    Timestamp = encouragementMessage.Created.UtcDateTime,
                    IsEdited = false
                };

                await _chatHubService.SendMessageToRoom(chatRoomId.ToString(), messageDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending bot message via SignalR");
            }
        }
    }
}
