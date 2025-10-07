using Chat_Support.Application.Chats.DTOs;
using Chat_Support.Application.Common.Interfaces;
// <<< اضافه کنید

namespace Chat_Support.Application.Chats.Commands;

public record EditMessageCommand(int MessageId, string NewContent) : IRequest<ChatMessageDto>;

public class EditMessageCommandHandler : IRequestHandler<EditMessageCommand, ChatMessageDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IChatHubService _chatHubService;
    private readonly IMapper _mapper; // <<< ۱. تزریق IMapper

    public EditMessageCommandHandler(IApplicationDbContext context, IUser user, IChatHubService chatHubService, IMapper mapper) // <<< ۲. اضافه کردن به کانستراکتور
    {
        _context = context;
        _user = user;
        _chatHubService = chatHubService;
        _mapper = mapper; // <<< ۳. مقداردهی اولیه
    }

    public async Task<ChatMessageDto> Handle(EditMessageCommand request, CancellationToken cancellationToken)
    {
        var userId = _user.Id;

        // ۱. خواندن پیام با تمام روابط لازم برای ساخت یک DTO کامل
        var message = await _context.ChatMessages
            .Include(m => m.Sender)
            .Include(m => m.ReplyToMessage).ThenInclude(rpm => rpm!.Sender) // برای اطلاعات ریپلای
            .Include(m => m.Reactions).ThenInclude(r => r.User) // برای اطلاعات ری‌اکشن‌ها
            .FirstOrDefaultAsync(m => m.Id == request.MessageId, cancellationToken);

        if (message == null)
            throw new KeyNotFoundException("Message not found.");

        if (message.SenderId != userId)
            throw new UnauthorizedAccessException("You can only edit your own messages.");

        // ۲. اعمال تغییرات بر روی انتیتی
        message.Content = request.NewContent;
        message.IsEdited = true;
        message.EditedAt = DateTime.Now;

        await _context.SaveChangesAsync(cancellationToken);

        // ۳. تبدیل انتیتی آپدیت شده به DTO با AutoMapper
        // ما userId را به مپینگ پاس می‌دهیم تا بتواند IsReactedByCurrentUser را محاسبه کند
        var messageDto = _mapper.Map<ChatMessageDto>(message, opts => opts.Items["currentUserId"] = userId);

        // ۴. ارسال آپدیت به کلاینت‌ها
        await _chatHubService.SendMessageUpdateToRoom(message.ChatRoomId.ToString(), messageDto, "MessageEdited");

        return messageDto;
    }
}
