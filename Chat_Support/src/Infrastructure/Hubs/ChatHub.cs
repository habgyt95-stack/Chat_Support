using System.Collections.Concurrent;
using Chat_Support.Application.Chats.DTOs;
using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Domain.Entities;
using Chat_Support.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Chat_Support.Infrastructure.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly ILogger<ChatHub> _logger;
    private readonly IPresenceTracker _presence;
    private static readonly ConcurrentDictionary<string, int> _typingUsers = new();

    public ChatHub(IApplicationDbContext context, IUser user, ILogger<ChatHub> logger, IPresenceTracker presence)
    {
        _context = context;
        _user = user;
        _logger = logger;
        _presence = presence;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = _user.Id;

        if (string.IsNullOrEmpty(userId.ToString()))
        {
            _logger.LogWarning("OnConnectedAsync: Missing user id. ConnectionId={ConnectionId}", Context.ConnectionId);
            Context.Abort();
            return;
        }

        // Register presence
        _presence.RegisterConnection(userId, Context.ConnectionId);

        // Save connection
        var connection = new UserConnection
        {
            UserId = userId,
            ConnectionId = Context.ConnectionId,
            ConnectedAt = DateTime.Now,
            IsActive = true
        };

        _context.UserConnections.Add(connection);
        await _context.SaveChangesAsync(CancellationToken.None);

        // Join user's chat rooms
        var chatRoomIds = await GetUserChatRoomIds(userId);
        foreach (var roomId in chatRoomIds)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId.ToString());
        }

        _logger.LogInformation("OnConnected: UserId={UserId} ConnectionId={ConnectionId} UserIdentifier={UserIdentifier} JoinedRooms={Rooms}", userId, Context.ConnectionId, Context.UserIdentifier, string.Join(',', chatRoomIds));

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = _user.Id;
        _logger.LogInformation("OnDisconnected: UserId={UserId} ConnectionId={ConnectionId} Error={Error}", userId, Context.ConnectionId, exception?.Message);

        // Remove presence
        _presence.UnregisterConnection(Context.ConnectionId);

        // Remove connection
        var connection = _context.UserConnections
            .FirstOrDefault(c => c.ConnectionId == Context.ConnectionId);

        if (connection != null)
        {
            connection.IsActive = false;
            await _context.SaveChangesAsync(CancellationToken.None);
        }

        // Stop typing indicators for this connection
        if (_typingUsers.TryRemove(Context.ConnectionId, out int roomIdWhenDisconnected))
        {
            _logger.LogDebug("Typing state removed for ConnectionId={ConnectionId} RoomId={RoomId}", Context.ConnectionId, roomIdWhenDisconnected);

            var user = await _context.KciUsers.FindAsync(userId);
            if (user != null)
            {
                var typingDto = new TypingIndicatorDto(
                    userId,
                    $"{user.FirstName} {user.LastName}",
                    roomIdWhenDisconnected,
                    false
                );
                await Clients.Group(roomIdWhenDisconnected.ToString()).SendAsync("UserTyping", typingDto);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinRoom(string roomId)
    {
        _logger.LogDebug("JoinRoom: ConnectionId={ConnectionId} RoomId={RoomId}", Context.ConnectionId, roomId);
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        if (int.TryParse(roomId, out var rid))
        {
            _presence.SetActiveRoom(Context.ConnectionId, rid);
        }
    }

    public async Task LeaveRoom(string roomId)
    {
        _logger.LogDebug("LeaveRoom: ConnectionId={ConnectionId} RoomId={RoomId}", Context.ConnectionId, roomId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
        _presence.ClearActiveRoom(Context.ConnectionId);
    }

    public async Task StartTyping(string roomIdStr)
    {
        var userId = _user.Id;
        if (string.IsNullOrEmpty(userId.ToString()) || !int.TryParse(roomIdStr, out int roomId))
            return;

        var user = await _context.KciUsers.FindAsync(userId);
        if (user == null) return;

        if (_typingUsers.TryGetValue(Context.ConnectionId, out int previousRoomId))
        {
            if (previousRoomId != roomId)
            {
                var previousTypingDto = new TypingIndicatorDto(userId, $"{user.FirstName} {user.LastName}", previousRoomId, false);
                await Clients.Group(previousRoomId.ToString()).SendAsync("UserTyping", previousTypingDto);
            }
        }

        _typingUsers.AddOrUpdate(Context.ConnectionId, roomId, (connId, oldRoomId) => roomId);
        _logger.LogTrace("StartTyping: UserId={UserId} ConnectionId={ConnectionId} RoomId={RoomId}", userId, Context.ConnectionId, roomId);
        _presence.SetActiveRoom(Context.ConnectionId, roomId);

        var typingDto = new TypingIndicatorDto(
            userId,
            $"{user.FirstName} {user.LastName}",
            roomId,
            true
        );

        await Clients.GroupExcept(roomId.ToString(), Context.ConnectionId)
            .SendAsync("UserTyping", typingDto);
    }

    public async Task StopTyping(string? roomIdStr = null)
    {
        var userId = _user.Id;
        if (string.IsNullOrEmpty(userId.ToString())) return;

        _presence.ClearActiveRoom(Context.ConnectionId);

        if (roomIdStr != null && int.TryParse(roomIdStr, out int roomIdFromParam))
        {
            if (_typingUsers.TryGetValue(Context.ConnectionId, out int currentTypingRoomId) && currentTypingRoomId == roomIdFromParam)
            {
                if (_typingUsers.TryRemove(Context.ConnectionId, out _))
                {
                    var user = await _context.KciUsers.FindAsync(userId);
                    if (user != null)
                    {
                        _logger.LogTrace("StopTyping(explicit): UserId={UserId} ConnectionId={ConnectionId} RoomId={RoomId}", userId, Context.ConnectionId, roomIdFromParam);
                        var typingDto = new TypingIndicatorDto(userId, $"{user.FirstName} {user.LastName}", roomIdFromParam, false);
                        await Clients.GroupExcept(roomIdFromParam.ToString(), Context.ConnectionId)
                            .SendAsync("UserTyping", typingDto);
                    }
                }
            }
        }
        else if (roomIdStr == null)
        {
            if (_typingUsers.TryRemove(Context.ConnectionId, out int currentTypingRoomId))
            {
                var user = await _context.KciUsers.FindAsync(userId);
                if (user != null)
                {
                    _logger.LogTrace("StopTyping(implicit): UserId={UserId} ConnectionId={ConnectionId} RoomId={RoomId}", userId, Context.ConnectionId, currentTypingRoomId);
                    var typingDto = new TypingIndicatorDto(userId, $"{user.FirstName} {user.LastName}", currentTypingRoomId, false);

                    await Clients.Group(currentTypingRoomId.ToString()).SendAsync("UserTyping", typingDto);
                }
            }
        }
    }

    public async Task MarkMessageAsRead(string messageId, string roomId)
    {
        var userId = _user.Id;
        if (!int.TryParse(messageId, out var msgId) || !int.TryParse(roomId, out var roomIdInt))
        {
            _logger.LogWarning("MarkMessageAsRead: Invalid ids. UserId={UserId} messageId={MessageId} roomId={RoomId}", userId, messageId, roomId);
            return;
        }

        var message = await _context.ChatMessages
            .FirstOrDefaultAsync(m => m.Id == msgId && m.ChatRoomId == roomIdInt);

        if (message == null || message.SenderId == userId)
        {
            _logger.LogDebug("MarkMessageAsRead: Skip. Message null or own message. UserId={UserId} MessageId={MessageId} RoomId={RoomId}", userId, msgId, roomIdInt);
            return;
        }

        var existingStatus = await _context.MessageStatuses
            .FirstOrDefaultAsync(s => s.MessageId == msgId && s.UserId == userId);

        var beforeStatus = existingStatus?.Status.ToString() ?? "<none>";
        if (existingStatus != null)
        {
            if (existingStatus.Status != ReadStatus.Read)
            {
                existingStatus.Status = ReadStatus.Read;
                existingStatus.StatusAt = DateTime.Now;
            }
        }
        else
        {
            var status = new MessageStatus
            {
                MessageId = msgId,
                UserId = userId,
                Status = ReadStatus.Read,
                StatusAt = DateTime.Now
            };
            _context.MessageStatuses.Add(status);
        }

        // آپدیت LastReadMessageId
        var chatRoomMember = await _context.ChatRoomMembers
            .FirstOrDefaultAsync(m => m.UserId == userId && m.ChatRoomId == roomIdInt);

        var beforeLastRead = chatRoomMember?.LastReadMessageId;
        if (chatRoomMember != null)
        {
            if (chatRoomMember.LastReadMessageId == null || msgId > chatRoomMember.LastReadMessageId)
            {
                chatRoomMember.LastReadMessageId = msgId;
            }
        }

        await _context.SaveChangesAsync(CancellationToken.None);
        _logger.LogInformation("MarkMessageAsRead: UserId={UserId} RoomId={RoomId} MessageId={MessageId} StatusBefore={BeforeStatus} LastReadBefore={BeforeLastRead} LastReadAfter={AfterLastRead}",
            userId, roomIdInt, msgId, beforeStatus, beforeLastRead, chatRoomMember?.LastReadMessageId);

        // اطلاع به فرستنده پیام
        if (message.SenderId != null && message.SenderId != userId)
        {
            _logger.LogInformation("Send(MessageRead) -> SenderUserId={SenderId} ForMessageId={MessageId} RoomId={RoomId}", message.SenderId, msgId, roomIdInt);
            await Clients.User(message.SenderId.ToString() ?? string.Empty)
                .SendAsync("MessageRead", new { MessageId = msgId, ReadBy = userId, ChatRoomId = roomIdInt });
        }

        // آپدیت unread count برای خود کاربر
        await UpdateUnreadCount(roomIdInt);
    }

    // New: receiver acknowledges that the message was delivered to their client
    public async Task AcknowledgeDelivered(string messageId, string roomId)
    {
        var userId = _user.Id;
        if (!int.TryParse(messageId, out var msgId) || !int.TryParse(roomId, out var roomIdInt))
        {
            _logger.LogWarning("AcknowledgeDelivered: Invalid ids. UserId={UserId} messageId={MessageId} roomId={RoomId}", userId, messageId, roomId);
            return;
        }

        var message = await _context.ChatMessages
            .FirstOrDefaultAsync(m => m.Id == msgId && m.ChatRoomId == roomIdInt);
        if (message == null)
        {
            _logger.LogWarning("AcknowledgeDelivered: Message not found. UserId={UserId} MessageId={MessageId} RoomId={RoomId}", userId, msgId, roomIdInt);
            return;
        }
        if (message.SenderId == userId)
        {
            _logger.LogDebug("AcknowledgeDelivered: Skip own message. UserId={UserId} MessageId={MessageId}", userId, msgId);
            return; // sender shouldn't ack
        }

        var status = await _context.MessageStatuses
            .FirstOrDefaultAsync(s => s.MessageId == msgId && s.UserId == userId);

        var beforeStatus = status?.Status.ToString() ?? "<none>";
        if (status == null)
        {
            status = new MessageStatus
            {
                MessageId = msgId,
                UserId = userId,
                Status = ReadStatus.Delivered,
                StatusAt = DateTime.Now
            };
            _context.MessageStatuses.Add(status);
        }
        else
        {
            // Only upgrade status (Delivered < Read)
            if (status.Status == ReadStatus.Sent)
            {
                status.Status = ReadStatus.Delivered;
                status.StatusAt = DateTime.Now;
            }
        }

        await _context.SaveChangesAsync(CancellationToken.None);
        _logger.LogInformation("AcknowledgeDelivered: UserId={UserId} RoomId={RoomId} MessageId={MessageId} StatusBefore={BeforeStatus} StatusAfter={AfterStatus}",
            userId, roomIdInt, msgId, beforeStatus, status.Status);

        // notify sender that this message is delivered
        if (message.SenderId != null && message.SenderId != userId)
        {
            _logger.LogInformation("Send(MessageDelivered) -> SenderUserId={SenderId} ForMessageId={MessageId} RoomId={RoomId}", message.SenderId, msgId, roomIdInt);
            await Clients.User(message.SenderId.ToString() ?? string.Empty)
                .SendAsync("MessageDelivered", new { MessageId = msgId, DeliveredBy = userId, ChatRoomId = roomIdInt });
        }
    }

    private async Task<List<int>> GetUserChatRoomIds(int? userId)
    {
        return await Task.Run(() =>
            _context.ChatRoomMembers
                .Where(m => m.UserId == userId && !m.IsDeleted)
                .Select(m => m.ChatRoomId)
                .ToList()
        );
    }

    public async Task UpdateUnreadCount(int roomId)
    {
        var userId = _user.Id;

        try
        {
            // محاسبه unread count برای این کاربر در این اتاق
            var lastReadMessage = await _context.ChatRoomMembers
                .Where(m => m.UserId == userId && m.ChatRoomId == roomId)
                .Select(m => m.LastReadMessageId)
                .FirstOrDefaultAsync();

            var unreadCount = await _context.ChatMessages
                .Where(m => m.ChatRoomId == roomId &&
                            m.SenderId != userId &&
                            !m.IsDeleted &&
                            (lastReadMessage == null || m.Id > lastReadMessage))
                .CountAsync();

            _logger.LogDebug("UpdateUnreadCount: UserId={UserId} RoomId={RoomId} LastRead={LastRead} UnreadCount={UnreadCount}", userId, roomId, lastReadMessage, unreadCount);

            // ارسال آپدیت به کلاینت‌های کاربر
            await Clients.User(userId.ToString() ?? string.Empty)
                .SendAsync("UnreadCountUpdate", new { RoomId = roomId, UnreadCount = unreadCount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating unread count. UserId={UserId} RoomId={RoomId}", userId, roomId);
        }
    }

    public async Task DeleteMessage(int messageId)
    {
        var userId = _user.Id;
        var message = await _context.ChatMessages
            .Include(m => m.ChatRoom)
            .FirstOrDefaultAsync(m => m.Id == messageId && m.SenderId == userId);

        if (message != null)
        {
            message.IsDeleted = true;
            message.Content = "[پیام حذف شد]";
            message.AttachmentUrl = null; // حذف attachment در صورت وجود

            await _context.SaveChangesAsync(CancellationToken.None);

            // Real-time broadcast به همه اعضای اتاق
            await Clients.Group(message.ChatRoomId.ToString())
                .SendAsync("MessageDeleted", new
                {
                    MessageId = messageId,
                    ChatRoomId = message.ChatRoomId,
                    IsDeleted = true,
                    Content = "[پیام حذف شد]"
                });
        }
    }

    public async Task NotifyNewMessage(int roomId, int messageId)
    {
        var userId = _user.Id;
        var message = await _context.ChatMessages
            .Include(m => m.Sender)
            .Include(m => m.ChatRoom)
            .FirstOrDefaultAsync(m => m.Id == messageId && m.ChatRoomId == roomId);
        if (message == null) return;

        // Send message to room (clients already handle ReceiveMessage), and then send room update per member
        var room = await _context.ChatRooms
            .Include(r => r.Members)
            .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(r => r.Id == roomId);
        if (room == null) return;

        foreach (var member in room.Members)
        {
            if (string.IsNullOrEmpty(member.UserId.ToString())) continue;
            var unreadCount = await _context.ChatMessages
                .CountAsync(m => m.ChatRoomId == roomId && m.SenderId != member.UserId && m.Id > (member.LastReadMessageId ?? 0));
            await Clients.User(member.UserId.ToString() ?? string.Empty)
                .SendAsync("UnreadCountUpdate", new { RoomId = roomId, UnreadCount = unreadCount });

            var roomUpdate = new
            {
                Id = room.Id,
                Name = room.Name,
                Avatar = room.Avatar,
                ChatRoomType = room.ChatRoomType,
                IsGroup = room.IsGroup,
                LastMessageContent = message.Content,
                LastMessageTime = message.Created.DateTime,
                UnreadCount = unreadCount
            };
            await Clients.User(member.UserId.ToString() ?? string.Empty)
                .SendAsync("ReceiveChatRoomUpdate", roomUpdate);
        }
    }

    public async Task MarkRoomAsRead(string roomIdStr)
    {
        var userId = _user.Id;
        if (!int.TryParse(roomIdStr, out var roomId))
        {
            _logger.LogWarning("MarkRoomAsRead: Invalid roomId. UserId={UserId} roomIdStr={RoomIdStr}", userId, roomIdStr);
            return;
        }

        var member = await _context.ChatRoomMembers.FirstOrDefaultAsync(m => m.ChatRoomId == roomId && m.UserId == userId);
        if (member == null)
        {
            _logger.LogWarning("MarkRoomAsRead: Membership not found. UserId={UserId} RoomId={RoomId}", userId, roomId);
            return;
        }

        var lastThreshold = member.LastReadMessageId ?? 0;

        // تمام پیام‌های ورودیِ خوانده نشده تا این لحظه
        var unreadIncoming = await _context.ChatMessages
            .Where(m => m.ChatRoomId == roomId && m.SenderId != userId && !m.IsDeleted && m.Id > lastThreshold)
            .OrderBy(m => m.Id)
            .ToListAsync();

        _logger.LogInformation("MarkRoomAsRead: UserId={UserId} RoomId={RoomId} LastThreshold={LastThreshold} UnreadCount={UnreadCount} UnreadIds={Ids}",
            userId, roomId, lastThreshold, unreadIncoming.Count, string.Join(',', unreadIncoming.Select(m => m.Id)));

        if (unreadIncoming.Count == 0)
        {
            await UpdateUnreadCount(roomId);
            return;
        }

        var now = DateTime.Now;
        foreach (var msg in unreadIncoming)
        {
            var status = await _context.MessageStatuses.FirstOrDefaultAsync(s => s.MessageId == msg.Id && s.UserId == userId);
            if (status == null)
            {
                status = new MessageStatus
                {
                    MessageId = msg.Id,
                    UserId = userId,
                    Status = ReadStatus.Read,
                    StatusAt = now
                };
                _context.MessageStatuses.Add(status);
            }
            else if (status.Status != ReadStatus.Read)
            {
                status.Status = ReadStatus.Read;
                status.StatusAt = now;
            }
        }

        // به‌روزرسانی آخرین پیام خوانده‌شده
        var beforeLastRead = member.LastReadMessageId;
        member.LastReadMessageId = Math.Max(member.LastReadMessageId ?? 0, unreadIncoming[^1].Id);

        await _context.SaveChangesAsync(CancellationToken.None);
        _logger.LogInformation("MarkRoomAsRead: Saved. UserId={UserId} RoomId={RoomId} LastReadBefore={Before} LastReadAfter={After}", userId, roomId, beforeLastRead, member.LastReadMessageId);

        // اطلاع به فرستنده هر پیام
        foreach (var msg in unreadIncoming)
        {
            if (msg.SenderId != null && msg.SenderId != userId)
            {
                _logger.LogInformation("Send(MessageRead) -> SenderUserId={SenderId} ForMessageId={MessageId} RoomId={RoomId}", msg.SenderId, msg.Id, roomId);
                await Clients.User(msg.SenderId.ToString() ?? string.Empty)
                    .SendAsync("MessageRead", new { MessageId = msg.Id, ReadBy = userId, ChatRoomId = roomId });
            }
        }
        // بروزرسانی شمارنده برای خود کاربر
        await UpdateUnreadCount(roomId);

        // ارسال آپدیت خلاصه روم برای خود کاربر (اختیاری اما کاربردی)
        var room = await _context.ChatRooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId);
        if (room != null)
        {
            await Clients.User(userId.ToString() ?? string.Empty).SendAsync("ReceiveChatRoomUpdate", new
            {
                Id = room.Id,
                Name = room.Name,
                Avatar = room.Avatar,
                ChatRoomType = room.ChatRoomType,
                IsGroup = room.IsGroup,
                UnreadCount = 0
            });
        }
    }
}
