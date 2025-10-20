using Chat_Support.Application.Chats.DTOs;
using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Application.Support.Services;
using Chat_Support.Domain.Entities;
using Chat_Support.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using AutoMapper;

namespace Chat_Support.Application.Support.Commands;

public record StartSupportChatCommand(
    int UserId,
    string? GuestSessionId,
    string? GuestName,
    string? GuestEmail,
    string? GuestPhone,
    string IpAddress,
    string? UserAgent,
    string InitialMessage,
    int? RegionId
) : IRequest<StartSupportChatResult>;

public record StartSupportChatResult(
    int ChatRoomId,
    int TicketId,
    int? AssignedAgentId,
    string? AssignedAgentName,
    List<ChatMessageDto>? Messages = null // پیام‌ها فقط برای اتاق جدید (پیام اولیه)
);

public class StartSupportChatCommandHandler : IRequestHandler<StartSupportChatCommand, StartSupportChatResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IAgentAssignmentService _agentAssignment;
    private readonly IChatHubService _chatHubService;
    private readonly IMapper _mapper;
    private readonly INewMessageNotifier _notifier;
    private readonly IVirtualBotService _virtualBotService;

    public StartSupportChatCommandHandler(
        IApplicationDbContext context,
        IAgentAssignmentService agentAssignment,
        IChatHubService chatHubService,
        IMapper mapper,
        INewMessageNotifier notifier,
        IVirtualBotService virtualBotService)
    {
        _context = context;
        _agentAssignment = agentAssignment;
        _chatHubService = chatHubService;
        _mapper = mapper;
        _notifier = notifier;
        _virtualBotService = virtualBotService;
    }

    public async Task<StartSupportChatResult> Handle(StartSupportChatCommand request, CancellationToken cancellationToken)
    {
        // 1. مدیریت Guest User
        GuestUser? guestUser = null;
        int? userId = request.UserId;

        if (userId == -1)
        {
            userId = null;
        }

        bool isGuest = string.IsNullOrEmpty(userId?.ToString()) || userId == null;
        if (isGuest)
        {
            guestUser = await _context.GuestUsers
                .FirstOrDefaultAsync(g => g.SessionId == request.GuestSessionId, cancellationToken);

            if (guestUser == null)
            {
                throw new UnauthorizedAccessException("Guest user not authenticated");
            }
            else
            {
                guestUser.LastActivityAt = DateTime.Now;
                guestUser.IsActive = true;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        // === جستجوی اتاق پشتیبانی فعال ===
        SupportTicket? existingTicket = null;
        ChatRoom? existingRoom = null;
        if (!isGuest)
        {
            existingTicket = await _context.SupportTickets
                .Include(t => t.ChatRoom)
                    .ThenInclude(r => r.Messages)
                        .ThenInclude(m => m.Sender)
                .Where(t => t.RequesterUserId == userId &&
                            (t.Status == SupportTicketStatus.Open || t.Status == SupportTicketStatus.InProgress) &&
                            t.ChatRoom.ChatRoomType == ChatRoomType.Support)
                .OrderByDescending(t => t.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }
        else if (guestUser != null)
        {
            existingTicket = await _context.SupportTickets
                .Include(t => t.ChatRoom)
                    .ThenInclude(r => r.Messages)
                        .ThenInclude(m => m.Sender)
                .Where(t => t.RequesterGuestId == guestUser.Id &&
                            (t.Status == SupportTicketStatus.Open || t.Status == SupportTicketStatus.InProgress) &&
                            t.ChatRoom.ChatRoomType == ChatRoomType.Support)
                .OrderByDescending(t => t.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (existingTicket != null && existingTicket.ChatRoom != null)
        {
            existingRoom = existingTicket.ChatRoom;

            // تاریخچه پیام‌ها را نیز برگردان
            var orderedMessages = existingRoom.Messages
                .OrderBy(m => m.Created)
                .ToList();
            var mappedMessages = _mapper.Map<List<ChatMessageDto>>(orderedMessages, opts => 
            {
                if (existingTicket.RequesterUserId.HasValue)
                    opts.Items["currentUserId"] = existingTicket.RequesterUserId.Value;
            });

            var assignedAgent = await _context.SupportAgents
                .FirstOrDefaultAsync(a => a.Id == existingTicket.AssignedAgentUserId, cancellationToken);

            return new StartSupportChatResult(
                existingRoom.Id,
                existingTicket.Id,
                assignedAgent?.UserId,
                assignedAgent != null && assignedAgent.User != null ? $"{assignedAgent.User.FirstName} {assignedAgent.User.LastName}" : null,
                mappedMessages
            );
        }

        // === اگر اتاق فعال نبود، روند فعلی ===
        
        // 2. اطمینان از وجود ربات مجازی (ایجاد خودکار در صورت نیاز)
        await _virtualBotService.EnsureVirtualBotExistsAsync(cancellationToken);
        
        // 3. پیدا کردن بهترین Agent (با رعایت منطقه؛ عدم fallback)
        // اگر هیچ agent واقعی در دسترس نبود، ربات برگردانده می‌شود
        var assignedAgentNew = await _agentAssignment.GetBestAvailableAgentAsync(request.RegionId, cancellationToken);

        // 4. ایجاد Chat Room
        var chatRoom = new ChatRoom
        {
            Name = !isGuest
                ? "پیام پشتیبانی - کاربر"
                : $"پیام پشتیبانی - {guestUser?.Name ?? request.GuestName ?? "Guest"}",
            Description = "پیام پشتیبانی زنده",
            IsGroup = false,
            ChatRoomType = ChatRoomType.Support, // تنظیم نوع به پشتیبانی
            CreatedById = isGuest ? null : userId,
            GuestIdentifier = guestUser?.SessionId,
            RegionId = request.RegionId
        };
        _context.ChatRooms.Add(chatRoom);
        await _context.SaveChangesAsync(cancellationToken);

        // 4. اضافه کردن Members
        if (!isGuest)
        {
            var exists = await _context.ChatRoomMembers.AnyAsync(m => m.UserId == request.UserId && m.ChatRoomId == chatRoom.Id, cancellationToken);
            if (exists == false)
            {
                _context.ChatRoomMembers.Add(new ChatRoomMember
                {
                    UserId = request.UserId,
                    ChatRoomId = chatRoom.Id,
                    Role = ChatRole.Member
                });
            }
        }
        else if (guestUser != null)
        {
            var exists = await _context.SupportGuestChatRoomMembers.AnyAsync(m => m.GuestUserId == guestUser.Id && m.ChatRoomId == chatRoom.Id, cancellationToken);
            if (exists == false)
            {
                _context.SupportGuestChatRoomMembers.Add(new SupportGuestChatRoomMember
                {
                    GuestUserId = guestUser.Id,
                    ChatRoomId = chatRoom.Id,
                    Role = ChatRole.Member
                });
            }
        }

        if (assignedAgentNew != null)
        {
            _context.ChatRoomMembers.Add(new ChatRoomMember
            {
                UserId = assignedAgentNew.UserId,
                ChatRoomId = chatRoom.Id,
                Role = ChatRole.Admin
            });
        }

        // 5. ایجاد Support Ticket
        var ticket = new SupportTicket
        {
            RequesterUserId = isGuest ? null : userId,
            RequesterGuestId = guestUser?.Id,
            AssignedAgentUserId = assignedAgentNew?.Id,
            ChatRoomId = chatRoom.Id,
            Status = SupportTicketStatus.Open,
            RegionId = request.RegionId
        };
        _context.SupportTickets.Add(ticket);

        // 6. ارسال پیام اولیه
        var initialMessageNew = new ChatMessage
        {
            Content = request.InitialMessage,
            SenderId = isGuest ? null : userId,
            ChatRoomId = chatRoom.Id,
            Type = MessageType.Text
        };
        _context.ChatMessages.Add(initialMessageNew);

        await _context.SaveChangesAsync(cancellationToken);

        // 7. Notify via SignalR
        if (assignedAgentNew != null)
        {
            await _chatHubService.NotifyAgentOfNewChat(assignedAgentNew.Id, chatRoom.Id);
        }

        // 8. Push notification برای Agent/اعضا (زمانی که در اتاق نیستند)
        await _notifier.NotifyAsync(initialMessageNew, chatRoom, guestUser, cancellationToken);

        // 9. اگر به ربات مجازی متصل شد، پیام خوش‌آمدگویی ارسال کن
        List<ChatMessageDto> initialMessages = new() 
        { 
            _mapper.Map<ChatMessageDto>(initialMessageNew, opts => 
            {
                // پیام اولیه از کاربر یا مهمان است
            }) 
        };

        if (assignedAgentNew != null && assignedAgentNew.IsVirtualBot)
        {
            // بررسی اینکه آیا همه پشتیبان‌های واقعی پر هستند یا هیچ کس آنلاین نیست
            var hasAnyAvailableRealAgent = await _context.SupportAgents
                .AnyAsync(a => !a.IsVirtualBot && 
                              a.IsActive && 
                              a.AgentStatus == AgentStatus.Available &&
                              a.CurrentActiveChats < a.MaxConcurrentChats, 
                              cancellationToken);

            string welcomeContent;
            if (!hasAnyAvailableRealAgent)
            {
                // هیچ پشتیبان واقعی آنلاین نیست
                welcomeContent = "سلام و درود! 👋\n\nمن دستیار مجازی پشتیبانی هستم. پیام شما با موفقیت دریافت شد. 📝\n\n" +
                                "در حال حاضر همه پشتیبانان ما مشغول هستند. نگران نباشید! به محض آنلاین شدن یکی از همکاران، " +
                                "بلافاصله به شما پاسخ خواهند داد. ⏰\n\n" +
                                "در این بین می‌توانید سوالات و جزئیات بیشتری بفرستید تا پشتیبان از موضوع کاملاً آگاه شود. 💬";
            }
            else
            {
                // همه پشتیبانان پر هستند اما آنلاین هستند
                welcomeContent = "سلام! 👋\n\nمن دستیار مجازی پشتیبانی هستم. پیام شما با موفقیت ثبت شد. 📝\n\n" +
                                "در حال حاضر تمام پشتیبانان ما در حال پاسخگویی به سایر کاربران هستند. شما در صف اولویت قرار گرفتید! 🎯\n\n" +
                                "به محض خالی شدن یکی از پشتیبانان، فوراً با شما در ارتباط خواهند بود. معمولاً این زمان کمتر از چند دقیقه است. ⏱️\n\n" +
                                "لطفاً کمی صبور باشید و در این بین می‌توانید توضیحات تکمیلی ارسال کنید. 😊";
            }

            var botWelcomeMessage = new ChatMessage
            {
                Content = welcomeContent,
                SenderId = assignedAgentNew.UserId,
                ChatRoomId = chatRoom.Id,
                Type = MessageType.Text
            };
            _context.ChatMessages.Add(botWelcomeMessage);
            await _context.SaveChangesAsync(cancellationToken);
            
            initialMessages.Add(_mapper.Map<ChatMessageDto>(botWelcomeMessage, opts => {}));
            
            // ارسال پیام ربات به SignalR
            await _chatHubService.SendMessageToRoom(chatRoom.Id.ToString(), initialMessages.Last());
        }

        return new StartSupportChatResult(
            chatRoom.Id,
            ticket.Id,
            assignedAgentNew?.UserId,
            assignedAgentNew != null && assignedAgentNew.User != null ? $"{assignedAgentNew.User.FirstName} {assignedAgentNew.User.LastName}" : null,
            initialMessages
        );
    }
}
