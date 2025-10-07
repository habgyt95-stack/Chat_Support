using Chat_Support.Domain.Entities;
using Chat_Support.Domain.Enums;

namespace Chat_Support.Application.Chats.DTOs;

// فرض بر این است که این DTO از قبل وجود دارد
public class ChatMessageDto
{
    public int Id { get; set; }
    public string Content { get; set; } = null!;
    public string SenderId { get; set; } = null!;
    public string SenderFullName { get; set; } = null!;
    public string? SenderAvatarUrl { get; set; }
    public int ChatRoomId { get; set; }
    public MessageType Type { get; set; }
    public string? AttachmentUrl { get; set; }
    public int? ReplyToMessageId { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsEdited { get; set; }
    public DateTime? EditedAt { get; set; }
    public string? RepliedMessageContent { get; set; }
    public string? RepliedMessageSenderName { get; set; }
    public MessageType? RepliedMessageType { get; set; }
    public List<ReactionInfo> Reactions { get; set; } = new();
    public ReadStatus DeliveryStatus { get; set; }

    // پروفایل مپینگ برای این DTO
    private class Mapping : Profile
    {
        public Mapping()
        {
            int currentUserId = 0;

            CreateMap<ChatMessage, ChatMessageDto>()
                .ForMember(dest => dest.SenderId,
                    opt => opt.MapFrom(src => src.SenderId.HasValue ? src.SenderId.Value.ToString() : string.Empty))
                .ForMember(dest => dest.SenderFullName,
                    opt => opt.MapFrom(src => src.Sender != null ? ($"{src.Sender.FirstName} {src.Sender.LastName}") : "مهمان"))
                .ForMember(dest => dest.SenderAvatarUrl,
                    opt => opt.MapFrom(src => src.Sender != null ? src.Sender.ImageName : null))
                // مهم: زمان را به صورت UTC ارسال می‌کنیم تا کلاینت آن را به ساعت محلی تبدیل کند
                .ForMember(dest => dest.Timestamp,
                    opt => opt.MapFrom(src => src.Created.UtcDateTime))
                .ForMember(dest => dest.RepliedMessageContent,
                    opt => opt.MapFrom(src => src.ReplyToMessage != null ? (src.ReplyToMessage.Content.Length > 70 ? src.ReplyToMessage.Content.Substring(0, 70) + "..." : src.ReplyToMessage.Content) : null))
                .ForMember(dest => dest.RepliedMessageSenderName,
                    opt => opt.MapFrom(src => src.ReplyToMessage != null && src.ReplyToMessage.Sender != null ? $"{src.ReplyToMessage.Sender.FirstName} {src.ReplyToMessage.Sender.LastName}" : null))
                .ForMember(dest => dest.RepliedMessageType,
                    opt => opt.MapFrom(src => src.ReplyToMessage != null ? (MessageType?)src.ReplyToMessage.Type : null))
                .ForMember(dest => dest.Reactions, opt => opt.MapFrom(src =>
                    src.Reactions
                        .GroupBy(r => r.Emoji) // گروه‌بندی بر اساس نوع اموجی
                        .Select(g => new ReactionInfo // ساختن یک DTO برای هر گروه اموجی
                        {
                            Emoji = g.Key,
                            Count = g.Count(), // تعداد کل ری‌اکشن‌های این اموجی
                            IsReactedByCurrentUser = g.Any(r => r.UserId == currentUserId), // آیا کاربر فعلی این اموجی را گذاشته؟
                            UserFullNames = g.Select(r => $"{r.User.FirstName} {r.User.LastName}").ToList() // لیست نام تمام واکنش‌دهندگان
                        })
                ));
        }
    }
}
