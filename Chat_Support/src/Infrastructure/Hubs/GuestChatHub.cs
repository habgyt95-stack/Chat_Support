using System.Collections.Concurrent;
using AutoMapper;
using Chat_Support.Application.Chats.DTOs;
using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Domain.Entities;
using Chat_Support.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

// <<< اضافه کنید

namespace Chat_Support.Infrastructure.Hubs;

[AllowAnonymous]
public class GuestChatHub : Hub
{
    private readonly IApplicationDbContext _context;
    private readonly IChatHubService _chatHubService;
    private readonly IMapper _mapper; // <<< ۱. تزریق IMapper
    private static readonly ConcurrentDictionary<string, string> _guestConnections = new();

    public GuestChatHub(IApplicationDbContext context, IChatHubService chatHubService, IMapper mapper) // <<< ۲. اضافه کردن به کانستراکتور
    {
        _context = context;
        _chatHubService = chatHubService;
        _mapper = mapper; // <<< ۳. مقداردهی اولیه
    }

    // متدهای OnConnectedAsync, OnDisconnectedAsync, JoinRoom بدون تغییر باقی می‌مانند
    public override async Task OnConnectedAsync()
    {
        // ... کد فعلی شما ...
        var httpContext = Context.GetHttpContext();
        var sessionId = httpContext?.Request.Query["access_token"].ToString()
            ?? httpContext?.Request.Headers["X-Session-Id"].ToString();

        if (string.IsNullOrEmpty(sessionId))
        {
            Context.Abort();
            return;
        }

        // Verify guest session
        var guestUser = await _context.GuestUsers
            .FirstOrDefaultAsync(g => g.SessionId == sessionId);

        if (guestUser == null)
        {
            // Create new guest user if not exists
            guestUser = new GuestUser
            {
                SessionId = sessionId,
                IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                UserAgent = httpContext?.Request.Headers["User-Agent"].ToString(),
                LastActivityAt = DateTime.Now,
                IsActive = true
            };
            _context.GuestUsers.Add(guestUser);
            await _context.SaveChangesAsync(CancellationToken.None);
        }
        else
        {
            // Update last activity
            guestUser.LastActivityAt = DateTime.Now;
            guestUser.IsActive = true;
            await _context.SaveChangesAsync(CancellationToken.None);
        }

        // Store connection mapping
        _guestConnections[Context.ConnectionId] = sessionId;

        // Join existing chat rooms for this guest
        var guestChatRooms = await _context.ChatRooms
            .Where(cr => cr.GuestIdentifier == sessionId)
            .Select(cr => cr.Id)
            .ToListAsync();

        foreach (var roomId in guestChatRooms)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId.ToString());
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_guestConnections.TryRemove(Context.ConnectionId, out var sessionId))
        {
            var guestUser = await _context.GuestUsers
                .FirstOrDefaultAsync(g => g.SessionId == sessionId);

            if (guestUser != null)
            {
                guestUser.LastActivityAt = DateTime.Now;
                guestUser.IsActive = false;
                await _context.SaveChangesAsync(CancellationToken.None);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinRoom(string roomId)
    {
        if (!_guestConnections.TryGetValue(Context.ConnectionId, out var sessionId))
            return;

        // Verify guest has access to this room
        var hasAccess = await _context.ChatRooms
            .AnyAsync(cr => cr.Id.ToString() == roomId && cr.GuestIdentifier == sessionId);

        if (hasAccess)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        }
    }


    public async Task SendMessage(int chatRoomId, string content)
    {
        if (!_guestConnections.TryGetValue(Context.ConnectionId, out var sessionId))
            return;

        var guestUser = await _context.GuestUsers
            .AsNoTracking() // بهینه‌سازی
            .FirstOrDefaultAsync(g => g.SessionId == sessionId);

        if (guestUser == null) return;

        var hasAccess = await _context.ChatRooms
            .AnyAsync(cr => cr.Id == chatRoomId && cr.GuestIdentifier == sessionId);

        if (!hasAccess) return;

        var message = new ChatMessage
        {
            Content = content,
            ChatRoomId = chatRoomId,
            Type = MessageType.Text,
            // SenderId برای مهمان null است
        };

        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync(CancellationToken.None);

        // --- بخش اصلی تغییرات ---
        // ۱. ابتدا با AutoMapper بخش‌های عمومی پیام را مپ می‌کنیم
        // چون SenderId در پیام null است، AutoMapper فیلدهای Sender... را خالی می‌گذارد
        var messageDto = _mapper.Map<ChatMessageDto>(message);

        // ۲. حالا اطلاعات خاص کاربر مهمان را به صورت دستی تنظیم می‌کنیم
        messageDto.SenderId = null!; // مهمانان شناسه کاربری ندارند
        messageDto.SenderFullName = guestUser.Name ?? "مهمان"; // از نام مهمان استفاده می‌کنیم
        messageDto.SenderAvatarUrl = null; // مهمان آواتار ندارد

        // ۳. پیام را برای اعضای گروه ارسال می‌کنیم
        await Clients.Group(chatRoomId.ToString()).SendAsync("ReceiveMessage", messageDto);
    }

    // متدهای StartTyping و StopTyping نیازی به تغییر ندارند چون از DTO دیگری استفاده می‌کنند
    // و خطای کانستراکتور مربوط به ChatMessageDto است.
    public async Task StartTyping(string roomId)
    {
        if (!_guestConnections.TryGetValue(Context.ConnectionId, out var sessionId))
            return;

        var guestUser = await _context.GuestUsers
            .FirstOrDefaultAsync(g => g.SessionId == sessionId);

        if (guestUser != null)
        {
            var typingIndicator = new TypingIndicatorDto
            (
                 null,
                 guestUser.Name ?? "Guest",
                 int.Parse(roomId),
                 true
            );

            await Clients.OthersInGroup(roomId).SendAsync("UserTyping", typingIndicator);
        }
    }

    public async Task StopTyping(string roomId)
    {
        if (!_guestConnections.TryGetValue(Context.ConnectionId, out var sessionId))
            return;

        var guestUser = await _context.GuestUsers
            .FirstOrDefaultAsync(g => g.SessionId == sessionId);

        if (guestUser != null)
        {
            var typingIndicator = new TypingIndicatorDto
            (
                 null,
                 guestUser.Name ?? "Guest",
                 int.Parse(roomId),
                 false
            );

            await Clients.OthersInGroup(roomId).SendAsync("UserTyping", typingIndicator);
        }
    }
}
