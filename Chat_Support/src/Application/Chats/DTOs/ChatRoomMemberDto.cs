using Chat_Support.Domain.Entities;

namespace Chat_Support.Application.Chats.DTOs;

public class ChatRoomMemberDto
{
    public int Id { get; set; }        // شناسه رکورد ChatRoomMember
    public string UserId { get; set; } = null!; // شناسه کاربر
    public string FullName { get; set; } = null!; // نام کامل کاربر
    public string? Avatar { get; set; }      // آواتار کاربر

    // پروفایل مپینگ در اینجا تعریف می‌شود
    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<ChatRoomMember, ChatRoomMemberDto>()
                .ForMember(dest => dest.UserId,
                    opt => opt.MapFrom(src => src.User.Id)) 
                .ForMember(dest => dest.FullName,
                    opt => opt.MapFrom(src => $"{src.User.FirstName} {src.User.LastName}")) 
                .ForMember(dest => dest.Avatar,
                    opt => opt.MapFrom(src => src.User.ImageName)); 
        }
    }
}
