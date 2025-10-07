using AutoMapper;
using Chat_Support.Application.Chats.DTOs;
using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Application.Support.Commands;
using Chat_Support.Domain.Entities;
using Chat_Support.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chat_Support.Web.Endpoints;

public class Support : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/support")
            .WithTags("Support");

        // Guest endpoints (no auth required)
        group.MapPost("/start", StartSupportChat)
            .AllowAnonymous()
            .WithName("StartSupportChat")
            .Produces<StartSupportChatResult>(StatusCodes.Status200OK)
            .RequireCors("ChatSupportApp");

        group.MapPost("/guest/message", SendGuestMessage)
            .AllowAnonymous()
            .AddEndpointFilter(async (context, next) =>
            {
                if (!context.HttpContext.Request.Headers.ContainsKey("X-Session-Id"))
                {
                    return Results.BadRequest("Missing required header: X-Session-Id");
                }
                return await next(context);
            });

        group.MapGet("/check-auth", (Delegate)CheckSupportAuth)
            .WithName("CheckSupportAuth")
            .RequireCors("ChatSupportApp");

        group.MapPost("/guest/auth", GuestAuth)
            .AllowAnonymous()
            .WithName("GuestAuth")
            .Produces<GuestAuthResult>(StatusCodes.Status200OK)
            .RequireCors("ChatSupportApp");

        // Agent endpoints (require auth)
        group.MapGet("/tickets", GetAgentTickets);

        group.MapPost("/tickets/{ticketId}/transfer", TransferTicket)
            .RequireAuthorization("Agent");

        group.MapPost("/agent/status", UpdateAgentStatus)
            .RequireAuthorization("Agent");

        group.MapGet("/agents/available", GetAvailableAgents)
            .RequireAuthorization("Agent");

        group.MapPost("/tickets/{ticketId}/close", CloseTicket)
            .RequireAuthorization("Agent");

        // New: check if current user is an agent (by DB lookup)
        group.MapGet("/is-agent", IsCurrentUserAgent)
            .RequireAuthorization();
    }

    private static Task<IResult> CheckSupportAuth(HttpContext context)
    {
        var isAuthenticated = context.User?.Identity?.IsAuthenticated == true;

        if (!isAuthenticated)
        {
            return Task.FromResult(Results.Json(new
            {
                IsAuthenticated = false,
                LoginUrl = "/login?returnUrl=" + Uri.EscapeDataString(context.Request.Path)
            }, statusCode: 401));
        }

        return Task.FromResult(Results.Ok(new { IsAuthenticated = true }));
    }

    private static async Task<IResult> StartSupportChat(
        HttpContext context,
        StartSupportChatRequest request,
        IMediator mediator,
        IApplicationDbContext dbContext)
    {
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        int? userId = null;
        var userIdString = (context.User?.Identity?.IsAuthenticated == true
            ? context.User.FindFirst("sub")?.Value
            : null) ?? string.Empty;

        if (!string.IsNullOrEmpty(userIdString) || !string.IsNullOrWhiteSpace(userIdString))
        {
            userId = int.TryParse(userIdString, out var parsedId) ? parsedId : null;
        }

        // اگر کاربر لاگین نکرده، و SessionId مهمان هم نیست، unauthorized
        if (string.IsNullOrEmpty(userId.ToString()) && string.IsNullOrEmpty(request.GuestSessionId))
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrEmpty(userId.ToString()))
        {
            // واکشی کاربر مهمان بر اساس SessionId
            GuestUser? guestUser = await dbContext.GuestUsers.FirstOrDefaultAsync(g => g.SessionId == request.GuestSessionId);
            if (guestUser == null)
            {
                return Results.Unauthorized(); // کاربر مهمان معتبر نیست
            }
        }

        // Force region override by explicit param or header
        int? regionId = request.ForceRegionId;
        if (!regionId.HasValue)
        {
            var headerVal = context.Request.Headers["X-Force-Region-Id"].ToString();
            if (int.TryParse(headerVal, out var ridHeader))
                regionId = ridHeader;
        }

        // اگر override نبود، از Origin/Referer برای نگاشت ناحیه استفاده کن
        if (!regionId.HasValue)
        {
            try
            {
                var originCandidate = request.OriginUrl
                    ?? context.Request.Headers["X-Origin-Url"].ToString()
                    ?? context.Request.Headers["Origin"].ToString()
                    ?? context.Request.Headers["Referer"].ToString();

                if (!string.IsNullOrWhiteSpace(originCandidate))
                {
                    string hostToMatch = Uri.TryCreate(originCandidate, UriKind.Absolute, out var originUri) ? originUri.Host : originCandidate;

                    // برای جلوگیری از خطای ترجمه EF Core، ابتدا داده‌ها را دریافت و سپس در حافظه مقایسه می‌کنیم
                    var regions = await dbContext.Regions
                        .AsNoTracking()
                        .Where(r => !string.IsNullOrEmpty(r.RelatedUri))
                        .ToListAsync();

                    var region = regions.FirstOrDefault(r =>
                        hostToMatch.Contains(r.RelatedUri!, StringComparison.OrdinalIgnoreCase)
                        || r.RelatedUri!.Contains(hostToMatch, StringComparison.OrdinalIgnoreCase));

                    regionId = region?.Id;
                }
            }
            catch
            {
                // ignored
            }
        }

        var command = new StartSupportChatCommand(
            userId ?? -1,
            request.GuestSessionId,
            request.GuestName,
            request.GuestEmail,
            request.GuestPhone,
            ipAddress,
            request.UserAgent ?? context.Request.Headers["User-Agent"].ToString(),
            request.InitialMessage ?? "پیام پشتیبانی جدید شروع شد",
            regionId
        );

        var result = await mediator.Send(command);
        return Results.Ok(result);
    }

    private static async Task<IResult> SendGuestMessage(
        HttpContext context,
        SendGuestMessageRequest request,
        IApplicationDbContext dbContext,
        IChatHubService chatHubService,
        IMapper mapper,
        INewMessageNotifier notifier) // <<< اضافه شد برای نوتیف
    {
        var sessionId = context.Request.Headers["X-Session-Id"].ToString();
        if (string.IsNullOrEmpty(sessionId))
            return Results.BadRequest("Session ID required");

        // Verify guest session
        var guestUser = await dbContext.GuestUsers
            .AsNoTracking() // بهینه‌سازی
            .FirstOrDefaultAsync(g => g.SessionId == sessionId);

        if (guestUser == null)
            return Results.Unauthorized();

        // Create message
        var message = new ChatMessage
        {
            Content = request.Content,
            ChatRoomId = request.ChatRoomId,
            Type = request.Type,
            AttachmentUrl = request.AttachmentUrl
            // SenderId برای مهمان null است
        };

        dbContext.ChatMessages.Add(message);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        // --- بخش اصلی تغییرات ---
        // ۲. ابتدا با AutoMapper بخش‌های عمومی پیام را مپ می‌کنیم
        var messageDto = mapper.Map<ChatMessageDto>(message);

        // ۳. سپس اطلاعات خاص کاربر مهمان را به صورت دستی تنظیم می‌کنیم
        messageDto.SenderId = null!; // مهمان شناسه کاربری استاندارد ندارد
        messageDto.SenderFullName = guestUser.Name ?? "مهمان";
        messageDto.SenderAvatarUrl = null; // مهمان آواتار ندارد

        // Broadcast via SignalR
        await chatHubService.SendMessageToRoom(
            request.ChatRoomId.ToString(),
            messageDto);

        // Push notification to room members who are not in the room now (agents)
        var chatRoom = await dbContext.ChatRooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == request.ChatRoomId);
        if (chatRoom != null)
        {
            await notifier.NotifyAsync(message, chatRoom, guestUser, CancellationToken.None);
        }

        return Results.Ok(messageDto);
    }

    private static async Task<IResult> GetAgentTickets(
        HttpContext context,
        IApplicationDbContext dbContext,
        [FromQuery] SupportTicketStatus? status)
    {
        string? value = context.User.FindFirst("sub")?.Value;
        if (value != null)
        {
            var agentId = int.Parse(value);

            var query = dbContext.SupportTickets
                .Include(t => t.RequesterUser)
                .Include(t => t.RequesterGuest)
                .Include(t => t.ChatRoom)
                .ThenInclude(cr => cr.Messages)
                .Where(t => t.AssignedAgent!.UserId == agentId);

            if (status.HasValue)
                query = query.Where(t => t.Status == status.Value);

            var tickets = await query
                .OrderByDescending(t => t.Created)
                .Select(t => new
                {
                    t.Id,
                    t.Status,
                    t.Created,
                    t.ClosedAt,
                    t.RegionId, // NEW
                    ChatRoomId = t.ChatRoomId,
                    RequesterName = t.RequesterUser != null
                        ? $"{t.RequesterUser.FirstName} {t.RequesterUser.LastName}"
                        : t.RequesterGuest!.Name ?? "Guest",
                    RequesterEmail = t.RequesterUser != null
                        ? t.RequesterUser.Email
                        : t.RequesterGuest!.Email,
                    LastMessage = t.ChatRoom.Messages
                        .OrderByDescending(m => m.Created)
                        .Select(m => new
                        {
                            m.Content,
                            m.Created,
                            SenderName = m.Sender != null
                                ? $"{m.Sender.FirstName} {m.Sender.LastName}"
                                : "Guest"
                        })
                        .FirstOrDefault(),
                    UnreadCount = t.ChatRoom.Messages
                        .Count(m => m.SenderId != agentId && m.Created > t.ChatRoom.Members
                            .Where(mem => mem.UserId == agentId)
                            .Select(mem => mem.LastSeenAt)
                            .FirstOrDefault())
                })
                .ToListAsync();

            return Results.Ok(tickets);
        }

        return Results.Empty;
    }

    private static async Task<IResult> TransferTicket(
        int ticketId,
        TransferTicketRequest request,
        IMediator mediator)
    {
        var command = new TransferChatCommand(
            ticketId,
            request.NewAgentId,
            request.Reason
        );

        var result = await mediator.Send(command);
        return result ? Results.Ok() : Results.NotFound();
    }

    private static async Task<IResult> UpdateAgentStatus(
        HttpContext context,
        UpdateAgentStatusRequest request,
        IAgentAssignmentService agentService)
    {
        var agentId = int.Parse(context.User.FindFirst("sub")?.Value!);
        await agentService.UpdateAgentStatusAsync(agentId, request.Status);
        return Results.Ok();
    }

    private static async Task<IResult> GetAvailableAgents(
        IApplicationDbContext dbContext,
        [FromQuery] int? regionId)
    {
        var agentsQuery = dbContext.SupportAgents
            .Include(a => a.User)
            .Where(a => a.AgentStatus != AgentStatus.Offline);

        if (regionId.HasValue)
        {
            var rid = regionId.Value;
            agentsQuery = agentsQuery.Where(a => a.User != null && (
                a.User.RegionId == rid ||
                a.User.UserRegions.Any(ur => ur.RegionId == rid)
            ));
        }

        var agents = await agentsQuery
            .Select(a => new
            {
                a.UserId,
                Name = a.User != null ? $"{a.User.FirstName} {a.User.LastName}" : "Agent",
                a.AgentStatus,
                a.CurrentActiveChats,
                a.MaxConcurrentChats,
                WorkloadPercentage = a.MaxConcurrentChats > 0
                    ? (a.CurrentActiveChats) * 100 / a.MaxConcurrentChats
                    : 0
            })
            .OrderBy(a => a.WorkloadPercentage)
            .ToListAsync();

        return Results.Ok(agents);
    }

    private static async Task<IResult> CloseTicket(
        int ticketId,
        CloseTicketRequest request,
        IApplicationDbContext dbContext,
        IChatHubService chatHubService)
    {
        var ticket = await dbContext.SupportTickets
            .Include(t => t.ChatRoom)
            .FirstOrDefaultAsync(t => t.Id == ticketId);

        if (ticket == null)
            return Results.NotFound();

        ticket.Status = SupportTicketStatus.Closed;
        ticket.ClosedAt = DateTime.Now;

        // Add closing message
        var closingMessage = new ChatMessage
        {
            Content = $"Chat closed: {request.Reason ?? "Resolved"}",
            ChatRoomId = ticket.ChatRoomId,
            Type = MessageType.System
        };
        dbContext.ChatMessages.Add(closingMessage);

        // Update agent active chats
        if (ticket.AssignedAgentUserId.HasValue)
        {
            var agent = await dbContext.SupportAgents.FirstOrDefaultAsync(a => a.UserId == ticket.AssignedAgentUserId.Value);
            if (agent is { CurrentActiveChats: > 0 })
            {
                agent.CurrentActiveChats--;
                if (agent.AgentStatus == AgentStatus.Busy &&
                    agent.CurrentActiveChats < agent.MaxConcurrentChats)
                {
                    agent.AgentStatus = AgentStatus.Available;
                }
            }
        }

        await dbContext.SaveChangesAsync(CancellationToken.None);

        // Notify via SignalR
        await chatHubService.SendMessageUpdateToRoom(
            ticket.ChatRoomId.ToString(),
            new { TicketId = ticketId, Status = "Closed" },
            "TicketClosed");

        return Results.Ok();
    }

    private static async Task<IResult> GuestAuth(
        [FromBody] GuestAuthRequest request,
        HttpContext context,
        IApplicationDbContext dbContext)
    {
        // اعتبارسنجی نام و تلفن
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest("Name is required");
        if (string.IsNullOrWhiteSpace(request.Phone))
            return Results.BadRequest("Phone is required");
        // اعتبارسنجی ساده شماره تلفن (می‌توانید Regex دقیق‌تر بگذارید)
        if (request.Phone.Length < 8 || request.Phone.Length > 20)
            return Results.BadRequest("Phone format is invalid");

        // جستجو بر اساس نام و تلفن
        var guestUser = await dbContext.GuestUsers
            .FirstOrDefaultAsync(g => g.Name == request.Name && g.Phone == request.Phone);

        if (guestUser == null)
        {
            // ایجاد کاربر مهمان جدید
            guestUser = new GuestUser
            {
                Name = request.Name,
                Phone = request.Phone,
                SessionId = Guid.NewGuid().ToString(),
                IpAddress = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                UserAgent = context.Request.Headers["User-Agent"].ToString(),
                LastActivityAt = DateTime.Now,
                IsActive = true
            };
            dbContext.GuestUsers.Add(guestUser);
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
        else
        {
            // اگر قبلاً وجود داشت، SessionId را به‌روزرسانی کن (یا همان قبلی را برگردان)
            guestUser.LastActivityAt = DateTime.Now;
            guestUser.IsActive = true;
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        return Results.Ok(new GuestAuthResult(
            guestUser.SessionId,
            guestUser.Name!,
            guestUser.Phone!
        ));
    }

    private static async Task<IResult> IsCurrentUserAgent(
        HttpContext context,
        IApplicationDbContext dbContext)
    {
        var sub = context.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(sub))
            return Results.Unauthorized();

        if (!int.TryParse(sub, out var userId))
            return Results.BadRequest("Invalid user id");

        var exists = await dbContext.SupportAgents.AsNoTracking().AnyAsync(a => a.UserId == userId);
        return Results.Ok(new { isAgent = exists });
    }

    // Request DTOs
    public record StartSupportChatRequest(
        string? GuestSessionId,
        string? GuestName,
        string? GuestEmail,
        string? GuestPhone,
        string? UserAgent,
        string? InitialMessage,
        string? OriginUrl,
        int? ForceRegionId
    );

    public record SendGuestMessageRequest(
        int ChatRoomId,
        string Content,
        MessageType Type = MessageType.Text,
        string? AttachmentUrl = null
    );

    public record TransferTicketRequest(
        int NewAgentId,
        string? Reason
    );

    public record UpdateAgentStatusRequest(
        AgentStatus Status
    );

    public record CloseTicketRequest(
        string? Reason
    );

    public record GuestAuthRequest(string Name, string Phone);
    public record GuestAuthResult(string SessionId, string Name, string Phone);
}
