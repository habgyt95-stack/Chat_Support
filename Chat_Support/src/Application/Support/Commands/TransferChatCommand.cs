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

    public TransferChatCommandHandler(
        IApplicationDbContext context,
        IChatHubService chatHubService)
    {
        _context = context;
        _chatHubService = chatHubService;
    }

    public async Task<bool> Handle(TransferChatCommand request, CancellationToken cancellationToken)
    {
        var ticket = await _context.SupportTickets
            .Include(t => t.ChatRoom)
                .ThenInclude(cr => cr.Members)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket == null)
            return false;

        var oldAgentId = ticket.AssignedAgentUserId;

        // Update ticket
        ticket.AssignedAgentUserId = request.NewAgentId;
        ticket.Status = SupportTicketStatus.Transferred;

        // Remove old agent from room
        if (!string.IsNullOrEmpty(oldAgentId.ToString()))
        {
            var oldMember = ticket.ChatRoom.Members
                .FirstOrDefault(m => m.UserId == oldAgentId);
            if (oldMember != null)
                _context.ChatRoomMembers.Remove(oldMember);
        }

        // Add new agent to room
        _context.ChatRoomMembers.Add(new ChatRoomMember
        {
            UserId = request.NewAgentId,
            ChatRoomId = ticket.ChatRoomId,
            Role = ChatRole.Admin
        });

        // Add system message
        var systemMessage = new ChatMessage
        {
            Content = $"Chat transferred to new agent{(string.IsNullOrEmpty(request.TransferReason) ? "" : $": {request.TransferReason}")}",
            ChatRoomId = ticket.ChatRoomId,
            Type = MessageType.System
        };
        _context.ChatMessages.Add(systemMessage);

        await _context.SaveChangesAsync(cancellationToken);

        // Notify via SignalR
        await _chatHubService.NotifyAgentOfNewChat(request.NewAgentId, ticket.ChatRoomId);
        if (!string.IsNullOrEmpty(oldAgentId.ToString()))
            await _chatHubService.NotifyChatTransferred((int)oldAgentId!, ticket.ChatRoomId);

        return true;
    }
}
