using Chat_Support.Application.Chats.DTOs;
using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Domain.Entities;
using Chat_Support.Domain.Enums;

namespace Chat_Support.Application.Support.Commands;

public record TransferChatCommand(
    int TicketId,
    int NewAgentId,
    string? TransferReason
) : IRequest<bool>;

public class TransferChatCommandHandler : IRequestHandler<TransferChatCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IChatHubService _chatHubService;
    private readonly IMapper _mapper;

    public TransferChatCommandHandler(
        IApplicationDbContext context,
        IChatHubService chatHubService,
        IMapper mapper)
    {
        _context = context;
        _chatHubService = chatHubService;
        _mapper = mapper;
    }

    public async Task<bool> Handle(TransferChatCommand request, CancellationToken cancellationToken)
    {
        var ticket = await _context.SupportTickets
            .Include(t => t.ChatRoom)
                .ThenInclude(cr => cr.Members)
                    .ThenInclude(m => m.User)
            .Include(t => t.Region)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket == null)
            return false;

        // Find the new agent by UserId (request.NewAgentId is actually UserId)
        var newAgentEntity = await _context.SupportAgents
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.UserId == request.NewAgentId, cancellationToken);

        if (newAgentEntity == null)
            return false;

        // Enforce region isolation: new agent must belong to ticket.RegionId
        if (ticket.RegionId.HasValue)
        {
            if (newAgentEntity.User == null)
                return false;

            var rid = ticket.RegionId.Value;
            var isInRegion = newAgentEntity.User.RegionId == rid || newAgentEntity.User.UserRegions.Any(ur => ur.RegionId == rid);
            if (!isInRegion)
                return false; // reject cross-region transfer
        }

        var oldAgentSupportAgentId = ticket.AssignedAgentUserId;

        // Update ticket - use SupportAgent.Id not UserId!
        ticket.AssignedAgentUserId = newAgentEntity.Id;  // ✅ FK به SupportAgent.Id
        ticket.Status = SupportTicketStatus.Transferred;

        // Remove old agent from room (need to find old agent's UserId for ChatRoomMember)
        if (oldAgentSupportAgentId.HasValue)
        {
            var oldAgent = await _context.SupportAgents
                .FirstOrDefaultAsync(a => a.Id == oldAgentSupportAgentId.Value, cancellationToken);
            
            if (oldAgent != null)
            {
                var oldMember = ticket.ChatRoom.Members
                    .FirstOrDefault(m => m.UserId == oldAgent.UserId);  // ChatRoomMember.UserId = KciUser.Id
                if (oldMember != null)
                    _context.ChatRoomMembers.Remove(oldMember);
            }
        }

        // Add new agent to room if not already (use newAgentEntity.UserId for ChatRoomMember)
        if (!ticket.ChatRoom.Members.Any(m => m.UserId == newAgentEntity.UserId))
        {
            _context.ChatRoomMembers.Add(new ChatRoomMember
            {
                UserId = newAgentEntity.UserId,  // ChatRoomMember.UserId = KciUser.Id
                ChatRoomId = ticket.ChatRoomId,
                Role = ChatRole.Admin
            });
        }

        // Add system message
        var systemMessage = new ChatMessage
        {
            Content = $"Chat transferred to new agent{(string.IsNullOrEmpty(request.TransferReason) ? string.Empty : $": {request.TransferReason}")}",
            ChatRoomId = ticket.ChatRoomId,
            Type = MessageType.System
        };
        _context.ChatMessages.Add(systemMessage);

        await _context.SaveChangesAsync(cancellationToken);

        // Broadcast system message to room (agents + guest)
        var savedMessage = await _context.ChatMessages
            .AsNoTracking()
            .Include(m => m.Sender)
            .FirstAsync(m => m.Id == systemMessage.Id, cancellationToken);
        var systemDto = _mapper.Map<ChatMessageDto>(savedMessage, opts => 
        {
            // سیستم پیام است، currentUserId را 0 یا هیچ مقداری ندارد
        });
        await _chatHubService.SendMessageToRoom(ticket.ChatRoomId.ToString(), systemDto);

        // Push room update to new agent to show the room instantly
        var roomDto = _mapper.Map<ChatRoomDto>(ticket.ChatRoom);
        roomDto.LastMessageContent = systemMessage.Content;
        roomDto.LastMessageTime = systemMessage.Created.DateTime;
        roomDto.LastMessageSenderName = "System";
        await _chatHubService.SendChatRoomUpdateToUser(newAgentEntity.UserId, roomDto);  // Use UserId for SignalR

        // Notify agents via specific events (optional clients can react by reloading rooms)
        await _chatHubService.NotifyAgentOfNewChat(newAgentEntity.UserId, ticket.ChatRoomId);  // Use UserId
        if (oldAgentSupportAgentId.HasValue)
        {
            var oldAgent = await _context.SupportAgents
                .FirstOrDefaultAsync(a => a.Id == oldAgentSupportAgentId.Value, cancellationToken);
            if (oldAgent != null)
            {
                await _chatHubService.NotifyChatTransferred(oldAgent.UserId, ticket.ChatRoomId);  // Use UserId
            }
        }

        return true;
    }
}
