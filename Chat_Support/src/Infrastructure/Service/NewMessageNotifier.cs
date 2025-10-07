using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chat_Support.Infrastructure.Service;

public class NewMessageNotifier : INewMessageNotifier
{
    private readonly IApplicationDbContext _db;
    private readonly IPresenceTracker _presence;
    private readonly IMessageNotificationService _notification;

    public NewMessageNotifier(IApplicationDbContext db, IPresenceTracker presence, IMessageNotificationService notification)
    {
        _db = db;
        _presence = presence;
        _notification = notification;
    }

    public async Task NotifyAsync(ChatMessage message, ChatRoom chatRoom, GuestUser? guestSender = null, CancellationToken cancellationToken = default)
    {
        // Determine recipients: all room members except the sender (if any)
        var members = await _db.ChatRoomMembers
            .Where(m => m.ChatRoomId == chatRoom.Id && !m.IsDeleted)
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken);

        int? senderId = message.SenderId;

        foreach (var userId in members)
        {
            if (senderId.HasValue && userId == senderId.Value) continue;

            var title = chatRoom.IsGroup
                ? chatRoom.Name ?? "????"
                : await GetPeerNameAsync(userId, chatRoom, guestSender, cancellationToken);

            var body = Truncate(message.Content ?? (message.AttachmentUrl != null ? "[Attachment]" : "???? ????"), 140);

            await _notification.SendNewMessageNotificationAsync(
                recipientUserId: userId,
                chatRoomId: chatRoom.Id,
                title: title,
                body: body,
                data: new Dictionary<string, string>
                {
                    ["type"] = chatRoom.IsGroup ? "group" : (chatRoom.ChatRoomType == Domain.Enums.ChatRoomType.Support ? "support" : "direct"),
                    ["roomId"] = chatRoom.Id.ToString(),
                    ["messageId"] = message.Id.ToString()
                },
                cancellationToken: cancellationToken);
        }
    }

    private async Task<string> GetPeerNameAsync(int recipientUserId, ChatRoom room, GuestUser? guestSender, CancellationToken ct)
    {
        if (room.ChatRoomType == Domain.Enums.ChatRoomType.Support && guestSender != null)
            return guestSender.Name ?? "?????";

        var otherMember = await _db.ChatRoomMembers
            .Include(m => m.User)
            .Where(m => m.ChatRoomId == room.Id && m.UserId != recipientUserId)
            .Select(m => m.User)
            .FirstOrDefaultAsync(ct);

        return otherMember != null ? $"{otherMember.FirstName} {otherMember.LastName}" : room.Name ?? "???? ????";
    }

    private static string Truncate(string text, int max)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        return text.Length <= max ? text : text.Substring(0, max) + "…";
    }
}
