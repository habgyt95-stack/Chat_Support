using Chat_Support.Application.Chats.DTOs;
using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Chat_Support.Infrastructure.Service;

public class ChatHubService : IChatHubService
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly IHubContext<GuestChatHub> _guestHubContext;

    public ChatHubService(IHubContext<ChatHub> hubContext, IHubContext<GuestChatHub> guestHubContext)
    {
        _hubContext = hubContext;
        _guestHubContext = guestHubContext;
    }

    public async Task SendMessageToRoom(string roomId, ChatMessageDto message)
    {
        // Send to authenticated users
        await _hubContext.Clients.Group(roomId)
            .SendAsync("ReceiveMessage", message);

        // Also send to guest clients joined to the same room on GuestChatHub
        await _guestHubContext.Clients.Group(roomId)
            .SendAsync("ReceiveMessage", message);
    }

    public async Task SendTypingIndicator(string roomId, TypingIndicatorDto indicator)
    {
        await _hubContext.Clients.Group(roomId)
            .SendAsync("UserTyping", indicator);

        await _guestHubContext.Clients.Group(roomId)
            .SendAsync("UserTyping", indicator);
    }

    public async Task NotifyUserOnline(Guid userId, bool isOnline)
    {
        await _hubContext.Clients.All
            .SendAsync("UserOnlineStatus", new { UserId = userId, IsOnline = isOnline });
    }

    public async Task SendChatRoomUpdateToUser(int userId, ChatRoomDto roomDetails)
    {
        await _hubContext.Clients.User(userId.ToString())
            .SendAsync("ReceiveChatRoomUpdate", roomDetails);
    }

    public async Task SendMessageUpdateToRoom(string roomId, object payload, string eventName = "MessageUpdated")
    {
        await _hubContext.Clients.Group(roomId).SendAsync(eventName, payload);
        await _guestHubContext.Clients.Group(roomId).SendAsync(eventName, payload);
    }

    public async Task NotifyAgentOfNewChat(int agentId, int chatRoomId)
    {
        await _hubContext.Clients.User(agentId.ToString())
            .SendAsync("NewSupportChat", new { ChatRoomId = chatRoomId });
    }

    public async Task NotifyChatTransferred(int oldAgentId, int chatRoomId)
    {
        await _hubContext.Clients.User(oldAgentId.ToString())
            .SendAsync("ChatTransferred", new { ChatRoomId = chatRoomId });
    }

    public async Task SendSupportChatUpdate(string connectionId, object update)
    {
        // Support updates target the guest's connection on GuestChatHub
        await _guestHubContext.Clients.Client(connectionId)
            .SendAsync("SupportChatUpdate", update);
    }
}
