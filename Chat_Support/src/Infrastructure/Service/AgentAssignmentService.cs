using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Domain.Entities;
using Chat_Support.Domain.Enums;

using Microsoft.EntityFrameworkCore;

namespace Chat_Support.Infrastructure.Service;

public class AgentAssignmentService : IAgentAssignmentService
{
    private readonly IApplicationDbContext _context;
    private readonly IChatHubService? _chatHubService;

    public AgentAssignmentService(IApplicationDbContext context, IChatHubService? chatHubService = null)
    {
        _context = context;
        _chatHubService = chatHubService;
    }

    public async Task<SupportAgent?> GetBestAvailableAgentAsync(CancellationToken cancellationToken = default)
    {
        // یافتن Agent های آنلاین با ظرفیت
        var availableAgent = await _context.SupportAgents
            .Where(a => a.IsActive
                && a.AgentStatus == AgentStatus.Available
                && a.CurrentActiveChats < a.MaxConcurrentChats)
            .OrderBy(a => a.CurrentActiveChats)
            .ThenBy(a => a.LastActivityAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (availableAgent != null)
        {
            availableAgent.CurrentActiveChats += 1;

            // اگر به حداکثر ظرفیت رسید، وضعیت را Busy کن
            if (availableAgent.CurrentActiveChats >= availableAgent.MaxConcurrentChats)
            {
                availableAgent.AgentStatus = AgentStatus.Busy;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        return availableAgent;
    }

    // NEW: Region-aware overload
    public async Task<SupportAgent?> GetBestAvailableAgentAsync(int? regionId = null, CancellationToken cancellationToken = default)
    {
        // اول سعی می‌کنیم agent واقعی (غیر ربات) پیدا کنیم
        IQueryable<SupportAgent> realAgentsQuery = _context.SupportAgents
            .Include(a => a.User)
            .Where(a => a.IsActive
                && !a.IsVirtualBot // فقط agent های واقعی
                && a.AgentStatus == AgentStatus.Available 
                && a.CurrentActiveChats < a.MaxConcurrentChats);

        if (regionId.HasValue)
        {
            var rid = regionId.Value;
            realAgentsQuery = realAgentsQuery.Where(a => a.User != null && (
                a.User.RegionId == rid ||
                a.User.UserRegions.Any(ur => ur.RegionId == rid)
            ));
        }

        var agent = await realAgentsQuery
            .OrderBy(a => a.CurrentActiveChats)
            .ThenBy(a => a.LastActivityAt)
            .FirstOrDefaultAsync(cancellationToken);

        // اگر هیچ agent واقعی پیدا نشد، ربات مجازی را برگردان
        if (agent == null)
        {
            agent = await _context.SupportAgents
                .Include(a => a.User)
                .Where(a => a.IsActive && a.IsVirtualBot)
                .FirstOrDefaultAsync(cancellationToken);
                
            // ربات را بدون افزایش counter برمی‌گردانیم (ظرفیت نامحدود دارد)
            return agent;
        }

        // برای agent واقعی، counter را افزایش می‌دهیم
        if (agent != null)
        {
            agent.CurrentActiveChats += 1;
            if (agent.CurrentActiveChats >= agent.MaxConcurrentChats)
            {
                agent.AgentStatus = AgentStatus.Busy;
            }
            await _context.SaveChangesAsync(cancellationToken);
        }

        return agent;
    }

    public async Task<int> GetAgentWorkloadAsync(int agentId, CancellationToken cancellationToken = default)
    {
        // agentId parameter is actually User.Id (UserId), but FK is to SupportAgent.Id
        var agent = await _context.SupportAgents.FirstOrDefaultAsync(a => a.UserId == agentId, cancellationToken);
        if (agent == null) return 0;
        
        return await _context.SupportTickets
            .CountAsync(t => t.AssignedAgentUserId == agent.Id  // ✅ FK به SupportAgent.Id
                && (t.Status == SupportTicketStatus.Open || t.Status == SupportTicketStatus.InProgress),
                cancellationToken);
    }

    public async Task UpdateAgentStatusAsync(int agentId, AgentStatus status, CancellationToken cancellationToken = default)
    {
        var agent = await _context.SupportAgents.FirstOrDefaultAsync(a => a.UserId == agentId, cancellationToken);
        if (agent != null)
        {
            var previousStatus = agent.AgentStatus;
            agent.AgentStatus = status;
            agent.LastActivityAt = DateTime.Now;

            if (status is AgentStatus.Offline or AgentStatus.Away)
            {
                // انتقال چت های فعال به Agent های دیگر
                var activeTickets = await _context.SupportTickets
                    .Include(t => t.ChatRoom)
                    .Where(t => t.AssignedAgentUserId == agent.Id  // ✅ FK به SupportAgent.Id
                        && (t.Status == SupportTicketStatus.Open || t.Status == SupportTicketStatus.InProgress))
                    .ToListAsync(cancellationToken);

                foreach (var ticket in activeTickets)
                {
                    // Try to find agent in the same region first
                    var newAgent = await GetBestAvailableAgentAsync(ticket.RegionId, cancellationToken);
                    
                    if (newAgent != null)
                    {
                        var oldAgentId = ticket.AssignedAgentUserId;
                        ticket.AssignedAgentUserId = newAgent.Id;  // ✅ باید Id باشه نه UserId!
                        // Keep status as InProgress instead of Transferred
                        if (ticket.Status == SupportTicketStatus.Open)
                            ticket.Status = SupportTicketStatus.InProgress;

                        // Add system message in Persian
                        var systemMessage = new ChatMessage
                        {
                            Content = "چت شما به پشتیبان دیگری منتقل شد. لطفاً منتظر بمانید.",
                            ChatRoomId = ticket.ChatRoomId,
                            Type = MessageType.System
                        };
                        _context.ChatMessages.Add(systemMessage);
                    }
                    else
                    {
                        // No available agent in region - add message that support will respond when available
                        var systemMessage = new ChatMessage
                        {
                            Content = "در حال حاضر پشتیبانی آنلاین در این ناحیه در دسترس نیست. به محض ورود پشتیبان، به شما پاسخ داده خواهد شد.",
                            ChatRoomId = ticket.ChatRoomId,
                            Type = MessageType.System
                        };
                        _context.ChatMessages.Add(systemMessage);
                        
                        // Set ticket to Open so it can be picked up by next available agent in the same region
                        ticket.Status = SupportTicketStatus.Open;
                        ticket.AssignedAgentUserId = null;
                    }
                }

                // Decrement agent's active chat count
                agent.CurrentActiveChats = 0;
            }
            else if (status == AgentStatus.Available && previousStatus != AgentStatus.Available)
            {
                // Agent just came online - بررسی و انتقال تیکت‌های ربات
                await ReassignBotTicketsToAvailableAgentsAsync(cancellationToken);
                
                // Agent just came online - assign any pending tickets matching agent region only
                var remainingCapacity = Math.Max(0, agent.MaxConcurrentChats - agent.CurrentActiveChats);
                if (remainingCapacity > 0)
                {
                    // ابتدا تیکت‌هایی که به ربات مجازی اختصاص داده شده را پیدا کنیم
                    var botTickets = await _context.SupportTickets
                        .Include(t => t.AssignedAgent)
                        .Where(t => 
                            t.AssignedAgent != null &&
                            t.AssignedAgent.IsVirtualBot &&
                            (t.Status == SupportTicketStatus.Open || t.Status == SupportTicketStatus.InProgress) &&
                            (
                                // Only tickets strictly in agent region(s)
                                _context.KciUsers
                                    .Where(u => u.Id == agentId)
                                    .Any(u => u.RegionId == t.RegionId || u.UserRegions.Any(ur => ur.RegionId == t.RegionId))
                            ))
                        .OrderBy(t => t.Created)
                        .Take(remainingCapacity)
                        .ToListAsync(cancellationToken);

                    // انتقال از ربات به agent واقعی
                    foreach (var ticket in botTickets)
                    {
                        if (agent.CurrentActiveChats < agent.MaxConcurrentChats)
                        {
                            var oldAgentSupportId = ticket.AssignedAgentUserId;
                            ticket.AssignedAgentUserId = agent.Id;  // ✅ باید Id باشه نه UserId
                            ticket.Status = SupportTicketStatus.InProgress;
                            agent.CurrentActiveChats++;
                            
                            // حذف ربات از ChatRoomMembers (اگر بود)
                            if (oldAgentSupportId.HasValue)
                            {
                                var oldBotAgent = await _context.SupportAgents
                                    .FirstOrDefaultAsync(a => a.Id == oldAgentSupportId.Value, cancellationToken);
                                
                                if (oldBotAgent != null && oldBotAgent.IsVirtualBot)
                                {
                                    var botMember = await _context.ChatRoomMembers
                                        .FirstOrDefaultAsync(m => m.ChatRoomId == ticket.ChatRoomId && m.UserId == oldBotAgent.UserId, cancellationToken);
                                    if (botMember != null)
                                    {
                                        _context.ChatRoomMembers.Remove(botMember);
                                    }
                                }
                            }

                            // اضافه کردن agent به ChatRoomMembers
                            var memberExists = await _context.ChatRoomMembers
                                .AnyAsync(m => m.ChatRoomId == ticket.ChatRoomId && m.UserId == agent.UserId, cancellationToken);
                            
                            if (!memberExists)
                            {
                                _context.ChatRoomMembers.Add(new ChatRoomMember
                                {
                                    UserId = agent.UserId,
                                    ChatRoomId = ticket.ChatRoomId,
                                    Role = ChatRole.Admin
                                });
                            }
                            
                            var systemMessage = new ChatMessage
                            {
                                Content = $"یک پشتیبان واقعی ({agent.User?.FirstName} {agent.User?.LastName}) به گفتگو متصل شد و از اینجا به بعد به شما پاسخ خواهد داد.",
                                ChatRoomId = ticket.ChatRoomId,
                                Type = MessageType.System
                            };
                            _context.ChatMessages.Add(systemMessage);
                        }
                        else
                        {
                            break;
                        }
                    }
                    
                    // حالا تیکت‌های بدون agent (اگر ظرفیت باقی مانده باشد)
                    remainingCapacity = Math.Max(0, agent.MaxConcurrentChats - agent.CurrentActiveChats);
                    if (remainingCapacity > 0)
                    {
                        var pendingTickets = await _context.SupportTickets
                            .Where(t => 
                                t.AssignedAgentUserId == null &&
                                t.Status == SupportTicketStatus.Open &&
                                (
                                    // Only tickets strictly in agent region(s)
                                    _context.KciUsers
                                        .Where(u => u.Id == agentId)
                                        .Any(u => u.RegionId == t.RegionId || u.UserRegions.Any(ur => ur.RegionId == t.RegionId))
                                ))
                            .OrderBy(t => t.Created)
                            .Take(remainingCapacity)
                            .ToListAsync(cancellationToken);

                        foreach (var ticket in pendingTickets)
                        {
                            if (agent.CurrentActiveChats < agent.MaxConcurrentChats)
                            {
                                ticket.AssignedAgentUserId = agent.Id;  // ✅ باید Id باشه نه UserId
                                ticket.Status = SupportTicketStatus.InProgress;
                                agent.CurrentActiveChats++;
                                
                                // اضافه کردن agent به ChatRoomMembers
                                var memberExists = await _context.ChatRoomMembers
                                    .AnyAsync(m => m.ChatRoomId == ticket.ChatRoomId && m.UserId == agent.UserId, cancellationToken);
                                
                                if (!memberExists)
                                {
                                    _context.ChatRoomMembers.Add(new ChatRoomMember
                                    {
                                        UserId = agent.UserId,
                                        ChatRoomId = ticket.ChatRoomId,
                                        Role = ChatRole.Admin
                                    });
                                }
                                
                                var systemMessage = new ChatMessage
                                {
                                    Content = "پشتیبان به چت متصل شد.",
                                    ChatRoomId = ticket.ChatRoomId,
                                    Type = MessageType.System
                                };
                                _context.ChatMessages.Add(systemMessage);
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// بررسی و انتقال تیکت‌های ربات به agent های فعال
    /// این متد باید به صورت دوره‌ای یا هنگام تغییر وضعیت صدا زده شود
    /// </summary>
    public async Task ReassignBotTicketsToAvailableAgentsAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"\n🤖 [{DateTime.Now:HH:mm:ss}] ReassignBotTickets: Starting reassignment check...");
        
        // بررسی تعداد پشتیبان‌های واقعی موجود
        var allAgents = await _context.SupportAgents
            .Include(a => a.User)
            .Where(a => !a.IsVirtualBot && a.IsActive)
            .ToListAsync(cancellationToken);
        
        Console.WriteLine($"👥 Total real agents: {allAgents.Count}");
        foreach (var ag in allAgents)
        {
            Console.WriteLine($"   - Agent {ag.UserId} ({ag.User?.FirstName} {ag.User?.LastName}): Status={ag.AgentStatus}, Active={ag.IsActive}, Chats={ag.CurrentActiveChats}/{ag.MaxConcurrentChats}");
        }
        
        var availableAgents = allAgents.Where(a => 
            a.AgentStatus == AgentStatus.Available && 
            a.CurrentActiveChats < a.MaxConcurrentChats).ToList();
        
        Console.WriteLine($"✅ Available agents: {availableAgents.Count}");
        
        // پیدا کردن تمام تیکت‌هایی که به ربات اختصاص دارند
        var botTickets = await _context.SupportTickets
            .Include(t => t.AssignedAgent)
            .Include(t => t.ChatRoom)
            .Where(t => 
                t.AssignedAgent != null &&
                t.AssignedAgent.IsVirtualBot &&
                (t.Status == SupportTicketStatus.Open || t.Status == SupportTicketStatus.InProgress))
            .OrderBy(t => t.Created)
            .ToListAsync(cancellationToken);

        Console.WriteLine($"🎫 Bot tickets found: {botTickets.Count}");
        
        if (botTickets.Any())
        {
            Console.WriteLine($"🔄 Found {botTickets.Count} bot ticket(s) to potentially reassign");
            foreach (var t in botTickets)
            {
                Console.WriteLine($"   - Ticket #{t.Id}: RegionId={t.RegionId}, Status={t.Status}, AssignedTo={t.AssignedAgent?.User?.FirstName}");
            }
        }
        else
        {
            Console.WriteLine($"✅ No bot tickets to reassign at this time.");
            return;
        }

        foreach (var ticket in botTickets)
        {
            Console.WriteLine($"\n🎯 Processing ticket #{ticket.Id} (RegionId: {ticket.RegionId})...");
            
            // پیدا کردن بهترین agent واقعی برای این تیکت (بدون ربات)
            var candidateAgents = await _context.SupportAgents
                .Include(a => a.User)
                .ThenInclude(u => u!.UserRegions)
                .Where(a => !a.IsVirtualBot && 
                           a.IsActive &&
                           a.AgentStatus == AgentStatus.Available &&
                           a.CurrentActiveChats < a.MaxConcurrentChats)
                .ToListAsync(cancellationToken);
            
            Console.WriteLine($"   🔍 Found {candidateAgents.Count} candidate agents (Available + has capacity)");
            
            // فیلتر بر اساس منطقه
            var regionMatchedAgents = candidateAgents.Where(a => 
                ticket.RegionId == null || 
                a.User!.RegionId == ticket.RegionId || 
                a.User!.UserRegions.Any(ur => ur.RegionId == ticket.RegionId)).ToList();
            
            Console.WriteLine($"   🌍 After region filter: {regionMatchedAgents.Count} agents match");
            
            var availableAgent = regionMatchedAgents
                .OrderBy(a => a.CurrentActiveChats)
                .FirstOrDefault();

            if (availableAgent != null)
            {
                var agentUserName = availableAgent.User != null 
                    ? $"{availableAgent.User.FirstName} {availableAgent.User.LastName}"
                    : "Unknown";
                Console.WriteLine($"✅ Transferring ticket #{ticket.Id} from bot to agent {availableAgent.UserId} ({agentUserName})");
                Console.WriteLine($"   ⚙️ Setting AssignedAgentUserId from {ticket.AssignedAgentUserId} to {availableAgent.Id} (SupportAgent.Id, NOT UserId={availableAgent.UserId})");
                
                // انتقال تیکت از ربات به agent واقعی
                var oldAgentSupportId = ticket.AssignedAgentUserId;
                ticket.AssignedAgentUserId = availableAgent.Id;  // ✅ باید Id باشه نه UserId! (Foreign Key به SupportAgent.Id)
                ticket.Status = SupportTicketStatus.InProgress;
                availableAgent.CurrentActiveChats++;

                if (availableAgent.CurrentActiveChats >= availableAgent.MaxConcurrentChats)
                {
                    availableAgent.AgentStatus = AgentStatus.Busy;
                }

                // حذف ربات از ChatRoomMembers (اگر وجود داشته باشد)
                if (oldAgentSupportId.HasValue)
                {
                    var oldBotAgent = await _context.SupportAgents
                        .FirstOrDefaultAsync(a => a.Id == oldAgentSupportId.Value, cancellationToken);
                    
                    if (oldBotAgent != null && oldBotAgent.IsVirtualBot)
                    {
                        var botMember = ticket.ChatRoom.Members
                            .FirstOrDefault(m => m.UserId == oldBotAgent.UserId);
                        if (botMember != null)
                        {
                            _context.ChatRoomMembers.Remove(botMember);
                            Console.WriteLine($"   🗑️ Removed bot (UserId={oldBotAgent.UserId}) from ChatRoom #{ticket.ChatRoomId}");
                        }
                    }
                }

                // اضافه کردن پشتیبان جدید به ChatRoomMembers
                if (!ticket.ChatRoom.Members.Any(m => m.UserId == availableAgent.UserId))
                {
                    var newMember = new ChatRoomMember
                    {
                        UserId = availableAgent.UserId,  // ChatRoomMember.UserId = KciUser.Id
                        ChatRoomId = ticket.ChatRoomId,
                        Role = ChatRole.Admin
                    };
                    _context.ChatRoomMembers.Add(newMember);
                    Console.WriteLine($"   ✅ Added agent (UserId={availableAgent.UserId}) to ChatRoom #{ticket.ChatRoomId}");
                }
                else
                {
                    Console.WriteLine($"   ℹ️ Agent (UserId={availableAgent.UserId}) already in ChatRoom #{ticket.ChatRoomId}");
                }

                // پیام سیستمی برای اطلاع کاربر
                var agentName = availableAgent.User != null 
                    ? $"{availableAgent.User.FirstName} {availableAgent.User.LastName}"
                    : "پشتیبان";
                
                var systemMessage = new ChatMessage
                {
                    Content = $"خبر خوب! یک پشتیبان ({agentName}) به گفتگوی شما متصل شد و از اینجا به بعد به سوالات شما پاسخ می‌دهد. 🎉",
                    ChatRoomId = ticket.ChatRoomId,
                    Type = MessageType.System
                };
                _context.ChatMessages.Add(systemMessage);

                await _context.SaveChangesAsync(cancellationToken);

                Console.WriteLine($"✅ Ticket #{ticket.Id} transferred successfully");
                
                // اطلاع رسانی از طریق SignalR
                if (_chatHubService != null)
                {
                    try
                    {
                        // ارسال پیام سیستمی به اتاق چت
                        var messageDto = new Application.Chats.DTOs.ChatMessageDto
                        {
                            Id = systemMessage.Id,
                            Content = systemMessage.Content,
                            SenderId = "0",
                            SenderFullName = "سیستم",
                            ChatRoomId = ticket.ChatRoomId,
                            Type = MessageType.System,
                            Timestamp = DateTime.UtcNow,
                            IsEdited = false
                        };
                        await _chatHubService.SendMessageToRoom(ticket.ChatRoomId.ToString(), messageDto);
                        
                        // اطلاع رسانی به agent جدید
                        await _chatHubService.NotifyAgentOfNewChat(availableAgent.UserId, ticket.ChatRoomId);
                        
                        Console.WriteLine($"📤 SignalR notifications sent for ticket #{ticket.Id}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Failed to send SignalR notifications: {ex.Message}");
                    }
                }
            }
            else
            {
                Console.WriteLine($"⏳ No available agent found for ticket #{ticket.Id} (RegionId: {ticket.RegionId})");
            }
        }
    }
}
