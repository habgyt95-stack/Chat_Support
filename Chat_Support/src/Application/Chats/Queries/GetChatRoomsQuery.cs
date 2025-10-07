using Chat_Support.Application.Chats.DTOs;
using Chat_Support.Application.Common.Interfaces;

// <<< اضافه کنید
// <<< اضافه کنید (برای ProjectTo)

// <<< اضافه کنید

namespace Chat_Support.Application.Chats.Queries;

public record GetChatRoomsQuery : IRequest<List<ChatRoomDto>>;

public class GetChatRoomsQueryHandler : IRequestHandler<GetChatRoomsQuery, List<ChatRoomDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IMapper _mapper; // <<< ۱. تزریق IMapper

    public GetChatRoomsQueryHandler(IApplicationDbContext context, IUser user, IMapper mapper) // <<< ۲. اضافه کردن به کانستراکتور
    {
        _context = context;
        _user = user;
        _mapper = mapper; // <<< ۳. مقداردهی اولیه
    }

    public async Task<List<ChatRoomDto>> Handle(GetChatRoomsQuery request, CancellationToken cancellationToken)
    {
        var userId = _user.Id;
        if (string.IsNullOrEmpty(userId.ToString()))
        {
            return new List<ChatRoomDto>();
        }

        // --- بخش ۱: کوئری بهینه برای خواندن تمام اطلاعات لازم ---
        var userChatRooms = await _context.ChatRooms
            .AsNoTracking()
            .Where(cr => cr.Members.Any(m => m.UserId == userId)) // فقط چت‌روم‌هایی که کاربر عضو آنهاست
            .Include(cr => cr.Members).ThenInclude(m => m.User) // برای نام و آواتار اعضا
            .Include(cr => cr.Messages.OrderByDescending(m => m.Created).Take(50)) // خواندن ۵۰ پیام آخر برای محاسبات
                .ThenInclude(m => m.Sender)
            .ToListAsync(cancellationToken);

        // --- بخش ۲: تبدیل کل لیست انتیتی‌ها به DTO با یک خط کد ---
        var resultDtoList = _mapper.Map<List<ChatRoomDto>>(userChatRooms);

        // --- بخش ۳: سفارشی‌سازی DTO ها با اطلاعات خاص کاربر ---
        // این حلقه اکنون روی DTO ها اجرا می‌شود و کوئری‌های کمتری به دیتابیس می‌زند
        foreach (var dto in resultDtoList)
        {
            // برای پیدا کردن انتیتی متناظر با DTO فعلی از لیست اولیه استفاده می‌کنیم
            var originalRoom = userChatRooms.First(r => r.Id == dto.Id);

            // پیدا کردن عضویت کاربر فعلی در این روم (بدون کوئری اضافه به دیتابیس)
            var currentUserMembership = originalRoom.Members.FirstOrDefault(m => m.UserId == userId);

            // محاسبه تعداد خوانده نشده‌ها (با استفاده از پیام‌های لود شده در حافظه)
            dto.UnreadCount = originalRoom.Messages
                .Count(m => m.SenderId != userId && m.Id > (currentUserMembership?.LastReadMessageId ?? 0));

            // تنظیم وضعیت Mute
            dto.IsMuted = currentUserMembership?.IsMuted ?? false;

            // سفارشی‌سازی نام و آواتار برای چت‌های خصوصی
            if (!originalRoom.IsGroup && originalRoom.Members.Count >= 2)
            {
                var otherMember = originalRoom.Members.FirstOrDefault(m => m.UserId != userId);
                if (otherMember?.User != null)
                {
                    dto.Name = $"{otherMember.User.FirstName} {otherMember.User.LastName}";
                    dto.Avatar = otherMember.User.ImageName;
                }
            }

            // محاسبه و تنظیم اطلاعات آخرین پیام
            var lastMessage = originalRoom.Messages.OrderByDescending(m => m.Created).FirstOrDefault();
            if (lastMessage != null)
            {
                dto.LastMessageContent = lastMessage.Content;
                dto.LastMessageTime = lastMessage.Created.DateTime;
                dto.LastMessageSenderName = $"{lastMessage.Sender.FirstName} {lastMessage.Sender.LastName}";
            }
        }

        // --- بخش ۴: مرتب‌سازی و بازگرداندن نتیجه نهایی ---
        return resultDtoList.OrderByDescending(r => r.LastMessageTime ?? r.CreatedAt).ToList();
    }
}
