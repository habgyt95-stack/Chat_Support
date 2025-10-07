using Chat_Support.Domain.Entities;
using Chat_Support.Domain.Enums;

namespace Chat_Support.Application.Chats.DTOs;

public class ChatRoomDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsGroup { get; set; }
    public string? Avatar { get; set; }
    public DateTime CreatedAt { get; set; }
    public ChatRoomType ChatRoomType { get; set; }
    public int UnreadCount { get; set; }
    public ICollection<ChatRoomMemberDto> Members { get; set; } = new List<ChatRoomMemberDto>();
    public string? LastMessageContent { get; set; }
    public DateTime? LastMessageTime { get; set; }
    public string? LastMessageSenderName { get; set; }
    public int MessageCount { get; set; }
    public bool IsMuted { get; set; }

    // پروفایل مپینگ برای این DTO
    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<ChatRoom, ChatRoomDto>()
                // اطمینان از UTC بودن فیلدهای تاریخ
                .ForMember(d => d.CreatedAt, opt => opt.MapFrom(s => s.Created.UtcDateTime))
                .ForMember(d => d.LastMessageTime, opt => opt.Ignore()); // LastMessageTime در نقاط مختلف تنظیم می‌شود
        }
    }
}
