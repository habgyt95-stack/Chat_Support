namespace Chat_Support.Application.Chats.DTOs;

public record ChatRoomUpdateDto(
    int RoomId,
    string? LastMessage,
    DateTime LastMessageTime,
    string? LastSenderName,
    int UnreadCount
);
