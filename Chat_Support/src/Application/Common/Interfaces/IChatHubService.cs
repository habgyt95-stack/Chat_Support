using Chat_Support.Application.Chats.DTOs;

namespace Chat_Support.Application.Common.Interfaces;

public interface IChatHubService
{
    Task SendMessageToRoom(string roomId, ChatMessageDto message);
    Task SendTypingIndicator(string roomId, TypingIndicatorDto indicator);
    Task NotifyUserOnline(Guid userId, bool isOnline);
    Task SendChatRoomUpdateToUser(int userId, ChatRoomDto roomDetails);
    Task SendMessageUpdateToRoom(string roomId, object payload, string eventName = "MessageUpdated");
    Task NotifyAgentOfNewChat(int agentId, int chatRoomId);
    Task NotifyChatTransferred(int oldAgentId, int chatRoomId);
    Task SendSupportChatUpdate(string connectionId, object update);
}
