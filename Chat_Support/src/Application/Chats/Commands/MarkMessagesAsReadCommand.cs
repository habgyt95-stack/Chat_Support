using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Domain.Entities;
using Chat_Support.Domain.Enums;

namespace Chat_Support.Application.Chats.Commands;

public record MarkMessagesAsReadCommand(
    int ChatRoomId,
    int? LastReadMessageId = null
) : IRequest<int>; // Returns count of marked messages

public class MarkMessagesAsReadCommandHandler : IRequestHandler<MarkMessagesAsReadCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IChatHubService _chatHubService;

    public MarkMessagesAsReadCommandHandler(
        IApplicationDbContext context,
        IUser user,
        IChatHubService chatHubService)
    {
        _context = context;
        _user = user;
        _chatHubService = chatHubService;
    }

    public async Task<int> Handle(MarkMessagesAsReadCommand request, CancellationToken cancellationToken)
    {
        var userId = _user.Id;

        // Get member record
        var member = await _context.ChatRoomMembers
            .FirstOrDefaultAsync(m => m.ChatRoomId == request.ChatRoomId && m.UserId == userId,
                cancellationToken);

        if (member == null)
            return 0;

        // Get unread messages
        var unreadMessages = await _context.ChatMessages
            .Where(m => m.ChatRoomId == request.ChatRoomId
                && m.SenderId != userId
                && m.Id > (member.LastReadMessageId ?? 0))
            .OrderBy(m => m.Id)
            .ToListAsync(cancellationToken);

        if (unreadMessages.Count == 0)
            return 0;

        // Update last read message ID
        var lastMessageId = request.LastReadMessageId ?? unreadMessages.Max(m => m.Id);
        member.LastReadMessageId = lastMessageId;
        member.LastSeenAt = DateTime.Now;

        // Create message status entries
        foreach (var message in unreadMessages.Where(m => m.Id <= lastMessageId))
        {
            // Check if status already exists
            var existingStatus = await _context.MessageStatuses
                .FirstOrDefaultAsync(ms => ms.MessageId == message.Id && ms.UserId == userId,
                    cancellationToken);

            if (existingStatus == null)
            {
                _context.MessageStatuses.Add(new MessageStatus
                {
                    MessageId = message.Id,
                    UserId = userId,
                    Status = ReadStatus.Read,
                    StatusAt = DateTime.Now
                });
            }
            else if (existingStatus.Status != ReadStatus.Read)
            {
                existingStatus.Status = ReadStatus.Read;
                existingStatus.StatusAt = DateTime.Now;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Notify message senders about read status
        var messagesBySender = unreadMessages
            .Where(m => m.Id <= lastMessageId)
            .GroupBy(m => m.SenderId);

        foreach (var group in messagesBySender)
        {
            if (!string.IsNullOrEmpty(group.Key.ToString()))
            {
                foreach (var message in group)
                {
                    await _chatHubService.SendMessageUpdateToRoom(
                        message.ChatRoomId.ToString(),
                        new
                        {
                            MessageId = message.Id,
                            ReadBy = userId,
                            ChatRoomId = message.ChatRoomId,
                            Status = ReadStatus.Read
                        },
                        "MessageRead");
                }
            }
        }

        return unreadMessages.Count(m => m.Id <= lastMessageId);
    }
}
