using Chat_Support.Application.Chats.DTOs;
using Chat_Support.Domain.Entities;
using Chat_Support.Domain.Enums;

namespace Chat_Support.Application.Admin.DTOs;

/// <summary>
/// DTO برای نمایش پیام‌ها در داشبورد ادمین
/// </summary>
public class AdminChatMessageDto
{
    public int Id { get; set; }
    public int ChatRoomId { get; set; }
    public int SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string? SenderPhone { get; set; }
    public string? SenderAvatar { get; set; }
    public string Content { get; set; } = string.Empty;
    public MessageType MessageType { get; set; }
    public string? FilePath { get; set; }
    public string? FileCaption { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsEdited { get; set; }
    public DateTime? EditedAt { get; set; }
    public bool IsDeleted { get; set; }
    
    // پیام Reply
    public int? ReplyToMessageId { get; set; }
    public string? ReplyToMessageContent { get; set; }
    public string? ReplyToSenderName { get; set; }
    
    // واکنش‌ها
    public List<ReactionInfo> Reactions { get; set; } = new();
    
    // وضعیت‌های خوانده شدن
    public List<AdminMessageStatusDto> ReadStatuses { get; set; } = new();
    
    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<ChatMessage, AdminChatMessageDto>()
                // Basic fields
                .ForMember(d => d.Timestamp, opt => opt.MapFrom(s => s.Created.UtcDateTime))
                .ForMember(d => d.EditedAt, opt => opt.MapFrom(s => s.LastModified.UtcDateTime))
                .ForMember(d => d.SenderId, opt => opt.MapFrom(s => s.SenderId ?? 0))
                .ForMember(d => d.MessageType, opt => opt.MapFrom(s => s.Type))
                .ForMember(d => d.FilePath, opt => opt.MapFrom(s => s.AttachmentUrl))
                // Sender info
                .ForMember(d => d.SenderName, opt => opt.MapFrom(s => 
                    s.Sender != null ? $"{s.Sender.FirstName} {s.Sender.LastName}" : "مهمان"))
                .ForMember(d => d.SenderPhone, opt => opt.MapFrom(s => s.Sender != null ? s.Sender.Mobile : null))
                .ForMember(d => d.SenderAvatar, opt => opt.MapFrom(s => s.Sender != null ? s.Sender.ImageName : null))
                // Reply info
                .ForMember(d => d.ReplyToMessageContent, opt => opt.MapFrom(s => s.ReplyToMessage != null ? s.ReplyToMessage.Content : null))
                .ForMember(d => d.ReplyToSenderName, opt => opt.MapFrom(s => 
                    s.ReplyToMessage != null && s.ReplyToMessage.Sender != null 
                        ? $"{s.ReplyToMessage.Sender.FirstName} {s.ReplyToMessage.Sender.LastName}" 
                        : null))
                // Collections
                .ForMember(d => d.ReadStatuses, opt => opt.MapFrom(s => s.Statuses))
                // We'll populate reactions manually in the query handler to avoid AutoMapper type map issues
                .ForMember(d => d.Reactions, opt => opt.Ignore());
        }
    }
}

/// <summary>
/// DTO برای نمایش وضعیت خوانده شدن پیام توسط کاربران
/// </summary>
public class AdminMessageStatusDto
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? UserPhone { get; set; }
    public ReadStatus Status { get; set; }
    public DateTime? ReadAt { get; set; }
    
    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<MessageStatus, AdminMessageStatusDto>()
                .ForMember(d => d.UserName, opt => opt.MapFrom(s => 
                    s.User != null ? $"{s.User.FirstName} {s.User.LastName}" : "نامشخص"))
                .ForMember(d => d.UserPhone, opt => opt.MapFrom(s => s.User != null ? s.User.Mobile : null))
                .ForMember(d => d.ReadAt, opt => opt.MapFrom(s => s.LastModified.UtcDateTime));
        }
    }
}
