using AutoMapper;
using Chat_Support.Application.Chats.DTOs;
using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Application.Support.Commands;
using Chat_Support.Application.Support.Queries;
using Chat_Support.Domain.Entities;
using Chat_Support.Domain.Enums;
using Chat_Support.Web.Infrastructure.Filters;
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

        // Agent endpoints (require auth - checked via Support_Agents table)
        group.MapGet("/tickets", GetAgentTickets)
            .RequireAuthorization()
            .AddEndpointFilter<AgentOnlyFilter>();

        group.MapPost("/tickets/{ticketId}/transfer", TransferTicket)
            .RequireAuthorization()
            .AddEndpointFilter<AgentOnlyFilter>();

        group.MapPost("/agent/status", UpdateAgentStatus)
            .RequireAuthorization()
            .AddEndpointFilter<AgentOnlyFilter>();

        group.MapGet("/agent/status-info", GetAgentStatusInfo)
            .RequireAuthorization()
            .AddEndpointFilter<AgentOnlyFilter>();

        group.MapGet("/agents/available", GetAvailableAgents)
            .RequireAuthorization()
            .AddEndpointFilter<AgentOnlyFilter>();

        group.MapPost("/tickets/{ticketId}/close", CloseTicket)
            .RequireAuthorization()
            .AddEndpointFilter<AgentOnlyFilter>();

        // New: check if current user is an agent (by DB lookup)
        group.MapGet("/is-agent", IsCurrentUserAgent)
            .RequireAuthorization();

        // Agent Management endpoints (admin only)
        group.MapGet("/agents", GetAllAgents)
            .RequireAuthorization(); // TODO: Add admin policy

        group.MapPost("/agents", CreateAgent)
            .RequireAuthorization(); // TODO: Add admin policy

        group.MapPut("/agents/{agentId}", UpdateAgent)
            .RequireAuthorization(); // TODO: Add admin policy

        group.MapDelete("/agents/{agentId}", DeleteAgent)
            .RequireAuthorization(); // TODO: Add admin policy

        // Admin: get tickets for a specific agent (by SupportAgent.Id)
        group.MapGet("/agents/{agentId:int}/tickets", GetTicketsByAgentId)
            .RequireAuthorization(); // TODO: Add admin policy

        // Admin: get ticket details
        group.MapGet("/tickets/{ticketId:int}", GetTicketDetails)
            .RequireAuthorization(); // TODO: Add admin policy
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
            userId = int.TryParse(userIdString, out var parsedId) ? parsedId : (int?)null;
        }

        // اگر کاربر لاگین نکرده، و SessionId مهمان هم نیست، unauthorized
        if (string.IsNullOrEmpty(userId?.ToString()) && string.IsNullOrEmpty(request.GuestSessionId))
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrEmpty(userId?.ToString()))
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
        SupportTicketStatus? status,
        IApplicationDbContext dbContext,
        IAgentStatusManager statusManager)
    {
        string? value = context.User.FindFirst("sub")?.Value;
        if (value != null)
        {
            var agentId = int.Parse(value);
            
            // به‌روزرسانی فعالیت agent
            await statusManager.UpdateActivityAsync(agentId);

            var query = dbContext.SupportTickets
                .Include(t => t.RequesterUser)
                .Include(t => t.RequesterGuest)
                .Include(t => t.ChatRoom)
                .Include(t => t.Region)
                .Where(t => t.AssignedAgent!.UserId == agentId);

            if (status.HasValue)
                query = query.Where(t => t.Status == status.Value);
            else
                // به صورت پیش‌فرض تیکت‌های بسته شده را نشان نده
                query = query.Where(t => t.Status != SupportTicketStatus.Closed);

            var tickets = await query
                .OrderByDescending(t => t.Created)
                .Select(t => new
                {
                    t.Id,
                    t.Status,
                    t.Created,
                    t.ClosedAt,
                    t.RegionId, // NEW
                    RegionTitle = t.Region != null ? (t.Region.Title ?? t.Region.Name) : null,
                    ChatRoomId = t.ChatRoomId,
                    RequesterName = t.RequesterUser != null
                        ? ($"{t.RequesterUser.FirstName} {t.RequesterUser.LastName}")
                        : (t.RequesterGuest != null ? (t.RequesterGuest.Name ?? "Guest") : "Guest"),
                    RequesterEmail = t.RequesterUser != null
                        ? t.RequesterUser.Email
                        : (t.RequesterGuest != null ? t.RequesterGuest.Email : null),
                    RequesterPhone = t.RequesterUser != null
                        ? t.RequesterUser.Mobile
                        : (t.RequesterGuest != null ? t.RequesterGuest.Phone : null),
                    LastMessage = t.ChatRoom.Messages
                        .OrderByDescending(m => m.Created)
                        .Select(m => new
                        {
                            m.Content,
                            m.Created,
                            SenderName = m.Sender != null
                                ? ($"{m.Sender.FirstName} {m.Sender.LastName}")
                                : "Guest"
                        })
                        .FirstOrDefault(),
                    UnreadCount = dbContext.ChatMessages
                        .Where(m => m.ChatRoomId == t.ChatRoomId && !m.IsDeleted && m.SenderId != agentId)
                        .Count(m => m.Id > (dbContext.ChatRoomMembers
                            .Where(mem => mem.ChatRoomId == t.ChatRoomId && mem.UserId == agentId)
                            .Select(mem => mem.LastReadMessageId)
                            .FirstOrDefault() ?? 0))
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
        IAgentStatusManager statusManager,
        IAgentAssignmentService agentAssignment,
        ILogger<Program> logger)
    {
        var agentId = int.Parse(context.User.FindFirst("sub")?.Value!);
        
        logger.LogInformation("Updating agent status: AgentId={AgentId}, NewStatus={NewStatus}", agentId, request.Status);
        Console.WriteLine($"🔄 [{DateTime.Now:HH:mm:ss}] UpdateAgentStatus API called: AgentId={agentId}, NewStatus={request.Status}");
        
        // تنظیم دستی وضعیت با TTL 15 دقیقه
        await statusManager.SetManualStatusAsync(agentId, request.Status);
        
        logger.LogInformation("Agent status updated successfully: AgentId={AgentId}", agentId);
        Console.WriteLine($"✅ [{DateTime.Now:HH:mm:ss}] Agent status updated in DB: AgentId={agentId}");
        
        // اگر وضعیت به Available تغییر کرد، تیکت‌های ربات را بررسی کن
        if (request.Status == AgentStatus.Available)
        {
            Console.WriteLine($"🚀 [{DateTime.Now:HH:mm:ss}] Agent became Available, triggering bot ticket reassignment...");
            try
            {
                await agentAssignment.ReassignBotTicketsToAvailableAgentsAsync();
                Console.WriteLine($"✅ [{DateTime.Now:HH:mm:ss}] Bot ticket reassignment completed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [{DateTime.Now:HH:mm:ss}] Error in bot ticket reassignment: {ex.Message}");
                logger.LogError(ex, "Error reassigning bot tickets after agent status change");
            }
        }
        
        return Results.Ok(new
        {
            Status = request.Status.ToString(),
            Message = "وضعیت شما به صورت دستی تنظیم شد و تا 15 دقیقه آینده معتبر خواهد بود.",
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        });
    }
    
    // Endpoint جدید برای دریافت اطلاعات کامل وضعیت
    private static async Task<IResult> GetAgentStatusInfo(
        HttpContext context,
        IAgentStatusManager statusManager)
    {
        var agentId = int.Parse(context.User.FindFirst("sub")?.Value!);
        var statusInfo = await statusManager.GetStatusInfoAsync(agentId);
        
        return Results.Ok(new
        {
            CurrentStatus = statusInfo.CurrentStatus.ToString(),
            IsManuallySet = statusInfo.IsManuallySet,
            ExpiresAt = statusInfo.ExpiresAt,
            TimeRemainingMinutes = statusInfo.TimeRemaining?.TotalMinutes,
            AutoDetectedStatus = statusInfo.AutoDetectedStatus.ToString(),
            LastActivityAt = statusInfo.LastActivityAt
        });
    }

    private static async Task<IResult> GetAvailableAgents(
        IApplicationDbContext dbContext,
        [FromQuery] int? regionId)
    {
        // Region isolation: require regionId for listing agents
        if (!regionId.HasValue)
        {
            return Results.Ok(new List<object>());
        }

        var rid = regionId.Value;

        var agentsQuery = dbContext.SupportAgents
            .Include(a => a.User)
            .Where(a => a.IsActive && a.AgentStatus != AgentStatus.Offline)
            .Where(a => a.User != null && (
                a.User.RegionId == rid ||
                a.User.UserRegions.Any(ur => ur.RegionId == rid)
            ));

        // Only show agents with capacity in the list
        agentsQuery = agentsQuery.Where(a => a.MaxConcurrentChats > 0);

        var agents = await agentsQuery
            .Select(a => new
            {
                a.UserId,
                Name = a.User != null ? ($"{a.User.FirstName} {a.User.LastName}") : "Agent",
                a.AgentStatus,
                a.CurrentActiveChats,
                a.MaxConcurrentChats,
                WorkloadPercentage = a.MaxConcurrentChats > 0
                    ? (a.CurrentActiveChats) * 100 / a.MaxConcurrentChats
                    : 0
            })
            .OrderBy(a => a.AgentStatus == AgentStatus.Available ? 0 : a.AgentStatus == AgentStatus.Away ? 1 : 2)
            .ThenBy(a => a.WorkloadPercentage)
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
            // ✅ AssignedAgentUserId به SupportAgent.Id اشاره داره نه UserId!
            var agent = await dbContext.SupportAgents.FirstOrDefaultAsync(a => a.Id == ticket.AssignedAgentUserId.Value);
            if (agent is { CurrentActiveChats: > 0 })
            {
                agent.CurrentActiveChats--;
                Console.WriteLine($"✅ Ticket #{ticketId} closed: Agent {agent.UserId} CurrentActiveChats decreased to {agent.CurrentActiveChats}");
                
                if (agent.AgentStatus == AgentStatus.Busy &&
                    agent.CurrentActiveChats < agent.MaxConcurrentChats)
                {
                    agent.AgentStatus = AgentStatus.Available;
                    Console.WriteLine($"✅ Agent {agent.UserId} status changed from Busy to Available");
                }
            }
            else if (agent == null)
            {
                Console.WriteLine($"⚠️ Agent not found for AssignedAgentUserId={ticket.AssignedAgentUserId.Value}");
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

    public record CreateAgentRequest(int UserId, int MaxConcurrentChats = 5);
    public record UpdateAgentRequest(bool? IsActive = null, int? MaxConcurrentChats = null);

    // Agent Management endpoint implementations
    private static async Task<IResult> GetAllAgents(IMediator mediator)
    {
        var query = new GetAllAgentsQuery();
        var result = await mediator.Send(query);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateAgent(
        CreateAgentRequest request,
        IMediator mediator)
    {
        var command = new CreateSupportAgentCommand(request.UserId, request.MaxConcurrentChats);
        var agentId = await mediator.Send(command);
        return Results.Ok(new { AgentId = agentId });
    }

    private static async Task<IResult> UpdateAgent(
        int agentId,
        UpdateAgentRequest request,
        IMediator mediator)
    {
        var command = new UpdateSupportAgentCommand(agentId, request.IsActive, request.MaxConcurrentChats);
        var success = await mediator.Send(command);
        return success ? Results.Ok(new { Success = true }) : Results.NotFound();
    }

    private static async Task<IResult> DeleteAgent(
        int agentId,
        IMediator mediator)
    {
        var command = new DeleteSupportAgentCommand(agentId);
        var success = await mediator.Send(command);
        return success ? Results.Ok(new { Success = true }) : Results.NotFound();
    }

    // Admin helper endpoints
    private static async Task<IResult> GetTicketsByAgentId(
        int agentId,
        [FromQuery] SupportTicketStatus? status,
        IApplicationDbContext db,
        IMapper mapper)
    {
        // agentId = SupportAgent.Id
        var agent = await db.SupportAgents
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == agentId);
        if (agent == null)
            return Results.NotFound("Agent not found");

        var q = db.SupportTickets
            .Include(t => t.RequesterUser)
            .Include(t => t.RequesterGuest)
            .Include(t => t.ChatRoom)
            .Include(t => t.Region)
            .Where(t => t.AssignedAgentUserId == agentId);

        if (status.HasValue)
            q = q.Where(t => t.Status == status.Value);

        var items = await q
            .OrderByDescending(t => t.Created)
            .Select(t => new
            {
                t.Id,
                t.Status,
                t.Created,
                t.ClosedAt,
                t.RegionId,
                RegionTitle = t.Region != null ? (t.Region.Title ?? t.Region.Name) : null,
                ChatRoomId = t.ChatRoomId,
                RequesterName = t.RequesterUser != null
                    ? ($"{t.RequesterUser.FirstName} {t.RequesterUser.LastName}")
                    : (t.RequesterGuest != null ? (t.RequesterGuest.Name ?? "Guest") : "Guest"),
                RequesterEmail = t.RequesterUser != null
                    ? t.RequesterUser.Email
                    : (t.RequesterGuest != null ? t.RequesterGuest.Email : null),
                RequesterPhone = t.RequesterUser != null
                    ? t.RequesterUser.Mobile
                    : (t.RequesterGuest != null ? t.RequesterGuest.Phone : null),
                LastMessage = t.ChatRoom.Messages
                    .Where(m => !m.IsDeleted)
                    .OrderByDescending(m => m.Created)
                    .Select(m => new { m.Content, m.Created })
                    .FirstOrDefault(),
                UnreadCount = db.ChatMessages
                    .Where(m => m.ChatRoomId == t.ChatRoomId && !m.IsDeleted && m.SenderId != agent.UserId)
                    .Count(m => m.Id > (db.ChatRoomMembers
                        .Where(mem => mem.ChatRoomId == t.ChatRoomId && mem.UserId == agent.UserId)
                        .Select(mem => mem.LastReadMessageId)
                        .FirstOrDefault() ?? 0))
            })
            .ToListAsync();

        return Results.Ok(new
        {
            Agent = new
            {
                agent.Id,
                agent.UserId,
                Name = agent.User != null ? ($"{agent.User.FirstName} {agent.User.LastName}") : null,
                agent.AgentStatus,
                agent.CurrentActiveChats,
                agent.MaxConcurrentChats
            },
            Tickets = items
        });
    }

    private static async Task<IResult> GetTicketDetails(
        int ticketId,
        IApplicationDbContext db)
    {
        var ticket = await db.SupportTickets
            .Include(t => t.RequesterUser)
            .Include(t => t.RequesterGuest)
            .Include(t => t.AssignedAgent)!.ThenInclude(a => a!.User)
            .Include(t => t.ChatRoom)
            .Include(t => t.Region)
            .FirstOrDefaultAsync(t => t.Id == ticketId);

        if (ticket == null)
            return Results.NotFound();

        return Results.Ok(new
        {
            ticket.Id,
            ticket.Status,
            ticket.Created,
            ticket.ClosedAt,
            ticket.RegionId,
            RegionTitle = ticket.Region != null ? (ticket.Region.Title ?? ticket.Region.Name) : null,
            ticket.ChatRoomId,
            Requester = ticket.RequesterUser != null
                ? new { Id = ticket.RequesterUser.Id, Name = (string?)($"{ticket.RequesterUser.FirstName} {ticket.RequesterUser.LastName}"), Email = (string?)ticket.RequesterUser.Email, Phone = (string?)ticket.RequesterUser.Mobile }
                : ticket.RequesterGuest != null ? new { Id = ticket.RequesterGuest.Id, Name = ticket.RequesterGuest.Name, Email = ticket.RequesterGuest.Email, Phone = ticket.RequesterGuest.Phone } : null,
            AssignedAgent = ticket.AssignedAgent != null
                ? new { ticket.AssignedAgent.Id, ticket.AssignedAgent.UserId, Name = ticket.AssignedAgent.User != null ? ($"{ticket.AssignedAgent.User.FirstName} {ticket.AssignedAgent.User.LastName}") : null }
                : null
        });
    }
}
