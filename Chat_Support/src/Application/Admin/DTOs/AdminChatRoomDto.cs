using Chat_Support.Domain.Entities;
using Chat_Support.Domain.Enums;

namespace Chat_Support.Application.Admin.DTOs;

/// <summary>
/// DTO کامل برای نمایش اطلاعات چت‌ها در داشبورد ادمین
/// </summary>
public class AdminChatRoomDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsGroup { get; set; }
    public string? Avatar { get; set; }
    public ChatRoomType ChatRoomType { get; set; }
    public int? RegionId { get; set; }
    public string? RegionName { get; set; }
    public bool IsDeleted { get; set; }
    
    // اطلاعات ایجادکننده
    public int? CreatedById { get; set; }
    public string? CreatedByName { get; set; }
    public string? CreatedByPhone { get; set; }
    
    // اطلاعات تاریخ
    public DateTime CreatedAt { get; set; }
    public DateTime? LastModifiedAt { get; set; }
    
    // آمار پیام‌ها
    public int TotalMessagesCount { get; set; }
    public DateTime? LastMessageTime { get; set; }
    public string? LastMessageContent { get; set; }
    public string? LastMessageSenderName { get; set; }
    
    // اطلاعات اعضا
    public int MembersCount { get; set; }
    public List<AdminChatRoomMemberDto> Members { get; set; } = new();
    
    // Guest Support
    public string? GuestIdentifier { get; set; }
    
    // پروفایل مپینگ
    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<ChatRoom, AdminChatRoomDto>()
                .ForMember(d => d.CreatedAt, opt => opt.MapFrom(s => s.Created.UtcDateTime))
                .ForMember(d => d.LastModifiedAt, opt => opt.MapFrom(s => s.LastModified.UtcDateTime))
                .ForMember(d => d.RegionName, opt => opt.MapFrom(s => s.Region != null ? s.Region.Name : null))
                .ForMember(d => d.CreatedByName, opt => opt.MapFrom(s => 
                    s.CreatedBy != null ? $"{s.CreatedBy.FirstName} {s.CreatedBy.LastName}" : null))
                .ForMember(d => d.CreatedByPhone, opt => opt.MapFrom(s => s.CreatedBy != null ? s.CreatedBy.Mobile : null))
                .ForMember(d => d.TotalMessagesCount, opt => opt.Ignore())
                .ForMember(d => d.LastMessageTime, opt => opt.Ignore())
                .ForMember(d => d.LastMessageContent, opt => opt.Ignore())
                .ForMember(d => d.LastMessageSenderName, opt => opt.Ignore())
                .ForMember(d => d.MembersCount, opt => opt.MapFrom(s => s.Members.Count));
        }
    }
}

/// <summary>
/// DTO برای نمایش اطلاعات اعضای چت در داشبورد ادمین
/// </summary>
public class AdminChatRoomMemberDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Avatar { get; set; }
    public DateTime JoinedAt { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsMuted { get; set; }
    public int? LastReadMessageId { get; set; }
    
    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<ChatRoomMember, AdminChatRoomMemberDto>()
                .ForMember(d => d.FullName, opt => opt.MapFrom(s => $"{s.User.FirstName} {s.User.LastName}"))
                .ForMember(d => d.PhoneNumber, opt => opt.MapFrom(s => s.User.Mobile))
                .ForMember(d => d.Avatar, opt => opt.MapFrom(s => s.User.ImageName))
                .ForMember(d => d.JoinedAt, opt => opt.MapFrom(s => s.Created.UtcDateTime));
        }
    }
}
