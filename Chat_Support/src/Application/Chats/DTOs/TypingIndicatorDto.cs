namespace Chat_Support.Application.Chats.DTOs;

public record TypingIndicatorDto(
    int? UserId,
    string UserFullName,
    int ChatRoomId,
    bool IsTyping
);
