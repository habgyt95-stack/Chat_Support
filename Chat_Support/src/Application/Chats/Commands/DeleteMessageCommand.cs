using Chat_Support.Application.Chats.DTOs;
using Chat_Support.Application.Common.Interfaces;
// <<< اضافه کنید

namespace Chat_Support.Application.Chats.Commands;

public record DeleteMessageCommand(int MessageId) : IRequest<bool>;

public class DeleteMessageCommandHandler : IRequestHandler<DeleteMessageCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IChatHubService _chatHubService;
    private readonly IMapper _mapper; // <<< ۱. تزریق IMapper

    public DeleteMessageCommandHandler(IApplicationDbContext context, IUser user, IChatHubService chatHubService, IMapper mapper) // <<< ۲. اضافه کردن به کانستراکتور
    {
        _context = context;
        _user = user;
        _chatHubService = chatHubService;
        _mapper = mapper; // <<< ۳. مقداردهی اولیه
    }

    public async Task<bool> Handle(DeleteMessageCommand request, CancellationToken cancellationToken)
    {
        var userId = _user.Id;

        // کوئری اولیه برای خواندن پیام و تمام روابط مورد نیاز
        var message = await _context.ChatMessages
            .Include(m => m.ChatRoom)
                .ThenInclude(cr => cr.Members)
                    .ThenInclude(crm => crm.User)
            .Include(m => m.ChatRoom)
                .ThenInclude(cr => cr.Messages.Where(msg => !msg.IsDeleted).OrderByDescending(msg => msg.Created)) // فقط پیام‌های حذف نشده
                    .ThenInclude(msg => msg.Sender)
            .FirstOrDefaultAsync(m => m.Id == request.MessageId, cancellationToken);

        if (message == null)
            throw new KeyNotFoundException("Message not found.");

        if (message.SenderId != userId)
            throw new UnauthorizedAccessException("You can only delete your own messages.");

        var chatRoom = message.ChatRoom;

        // حذف نرم پیام
        message.IsDeleted = true;
        message.Content = "[پیام حذف شد]";
        message.AttachmentUrl = null;
        await _context.SaveChangesAsync(cancellationToken);

        // اطلاع‌رسانی به کلاینت‌ها که این پیام حذف شده است
        await _chatHubService.SendMessageUpdateToRoom(chatRoom.Id.ToString(),
            new { MessageId = message.Id, ChatRoomId = chatRoom.Id, IsDeleted = true },
            "MessageDeleted");

        // =================================================================
        // بخش اصلی: آپدیت و ارسال وضعیت جدید چت‌روم با AutoMapper
        // =================================================================

        // پیدا کردن آخرین پیام جدید از بین پیام‌هایی که از قبل خوانده‌ایم
        var newLastMessage = chatRoom.Messages
            .Where(m => m.Id != message.Id) // پیام حذف شده را در نظر نگیر
            .OrderByDescending(m => m.Created)
            .FirstOrDefault();

        foreach (var member in chatRoom.Members)
        {
            if (string.IsNullOrEmpty(member.UserId.ToString())) continue;

            // ۱. ابتدا یک DTO کامل با استفاده از AutoMapper بسازید
            var roomUpdateDto = _mapper.Map<ChatRoomDto>(chatRoom);

            // ۲. حالا اطلاعات خاص هر کاربر و اطلاعات جدید را روی DTO آپدیت کنید
            roomUpdateDto.UnreadCount = chatRoom.Messages
                .Count(m => m.Id != message.Id &&
                            m.SenderId != member.UserId &&
                            m.Id > (member.LastReadMessageId ?? 0));

            // آپدیت اطلاعات آخرین پیام
            roomUpdateDto.LastMessageContent = newLastMessage?.Content;
            roomUpdateDto.LastMessageTime = newLastMessage?.Created.DateTime;
            roomUpdateDto.LastMessageSenderName = newLastMessage?.Sender != null ? $"{newLastMessage.Sender.FirstName} {newLastMessage.Sender.LastName}" : null;
            roomUpdateDto.MessageCount = chatRoom.Messages.Count(m => !m.IsDeleted && m.Id != message.Id);


            // سفارشی‌سازی نام و آواتار برای چت‌های خصوصی
            if (!chatRoom.IsGroup)
            {
                var otherUser = chatRoom.Members.FirstOrDefault(m => m.UserId != member.UserId)?.User;
                if (otherUser != null)
                {
                    roomUpdateDto.Name = $"{otherUser.FirstName} {otherUser.LastName}";
                    roomUpdateDto.Avatar = otherUser.ImageName;
                }
            }

            // ۳. DTO نهایی و سفارشی‌شده را برای هر کاربر ارسال کنید
            await _chatHubService.SendChatRoomUpdateToUser(member.UserId, roomUpdateDto);
        }

        return true;
    }
}
