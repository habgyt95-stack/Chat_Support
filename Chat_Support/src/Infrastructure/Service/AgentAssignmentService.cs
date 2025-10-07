using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Domain.Entities;
using Chat_Support.Domain.Enums;

using Microsoft.EntityFrameworkCore;

namespace Chat_Support.Infrastructure.Service;

public class AgentAssignmentService : IAgentAssignmentService
{
    private readonly IApplicationDbContext _context;

    public AgentAssignmentService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SupportAgent?> GetBestAvailableAgentAsync(CancellationToken cancellationToken = default)
    {
        // یافتن Agent های آنلاین با ظرفیت
        var availableAgent = await _context.SupportAgents
            .Where(a => a.AgentStatus == AgentStatus.Available
                && a.CurrentActiveChats < a.MaxConcurrentChats)
            .OrderBy(a => a.CurrentActiveChats)
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
        IQueryable<SupportAgent> query = _context.SupportAgents
            .Include(a => a.User)
            .Where(a => a.AgentStatus == AgentStatus.Available && a.CurrentActiveChats < a.MaxConcurrentChats);

        if (regionId.HasValue)
        {
            var rid = regionId.Value;
            query = query.Where(a => a.User != null && (
                a.User.RegionId == rid ||
                a.User.UserRegions.Any(ur => ur.RegionId == rid)
            ));
        }

        var agent = await query
            .OrderBy(a => a.CurrentActiveChats)
            .FirstOrDefaultAsync(cancellationToken);

        // Fallback to any available agent if none in region
        if (agent == null && regionId.HasValue)
        {
            return await GetBestAvailableAgentAsync(cancellationToken);
        }

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
        return await _context.SupportTickets
            .CountAsync(t => t.AssignedAgentUserId == agentId
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
                    .Where(t => t.AssignedAgentUserId == agentId
                        && (t.Status == SupportTicketStatus.Open || t.Status == SupportTicketStatus.InProgress))
                    .ToListAsync(cancellationToken);

                foreach (var ticket in activeTickets)
                {
                    // Try to find agent in the same region first
                    var newAgent = await GetBestAvailableAgentAsync(ticket.RegionId, cancellationToken);
                    
                    if (newAgent != null)
                    {
                        var oldAgentId = ticket.AssignedAgentUserId;
                        ticket.AssignedAgentUserId = newAgent.UserId;
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
                        // No available agent - add message that support will respond when available
                        var systemMessage = new ChatMessage
                        {
                            Content = "در حال حاضر پشتیبانی آنلاین در دسترس نیست. به محض ورود پشتیبان، به شما پاسخ داده خواهد شد.",
                            ChatRoomId = ticket.ChatRoomId,
                            Type = MessageType.System
                        };
                        _context.ChatMessages.Add(systemMessage);
                        
                        // Set ticket to Open so it can be picked up by next available agent
                        ticket.Status = SupportTicketStatus.Open;
                        ticket.AssignedAgentUserId = null;
                    }
                }

                // Decrement agent's active chat count
                agent.CurrentActiveChats = 0;
            }
            else if (status == AgentStatus.Available && previousStatus != AgentStatus.Available)
            {
                // Agent just came online - assign any pending tickets
                var pendingTickets = await _context.SupportTickets
                    .Where(t => t.AssignedAgentUserId == null 
                        && t.Status == SupportTicketStatus.Open
                        && t.RegionId == null || 
                        _context.KciUsers
                            .Where(u => u.Id == agentId && 
                                (u.RegionId == t.RegionId || u.UserRegions.Any(ur => ur.RegionId == t.RegionId)))
                            .Any())
                    .OrderBy(t => t.Created)
                    .Take(agent.MaxConcurrentChats)
                    .ToListAsync(cancellationToken);

                foreach (var ticket in pendingTickets)
                {
                    if (agent.CurrentActiveChats < agent.MaxConcurrentChats)
                    {
                        ticket.AssignedAgentUserId = agentId;
                        ticket.Status = SupportTicketStatus.InProgress;
                        agent.CurrentActiveChats++;
                        
                        var systemMessage = new ChatMessage
                        {
                            Content = "پشتیبان به چت متصل شد.",
                            ChatRoomId = ticket.ChatRoomId,
                            Type = MessageType.System
                        };
                        _context.ChatMessages.Add(systemMessage);
                    }
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
