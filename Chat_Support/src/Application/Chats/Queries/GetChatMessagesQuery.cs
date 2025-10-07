using Chat_Support.Application.Chats.DTOs;
using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Domain.Enums;
using Microsoft.EntityFrameworkCore;

// <<< این using برای ProjectTo ضروری است

namespace Chat_Support.Application.Chats.Queries;

public record GetChatMessagesQuery(
    int ChatRoomId,
    int Page = 1,
    int PageSize = 50
) : IRequest<List<ChatMessageDto>>;

public class GetChatMessagesQueryHandler : IRequestHandler<GetChatMessagesQuery, List<ChatMessageDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly IUser _user;

    public GetChatMessagesQueryHandler(IApplicationDbContext context, IMapper mapper, IUser user)
    {
        _context = context;
        _mapper = mapper;
        _user = user;
    }

    public async Task<List<ChatMessageDto>> Handle(GetChatMessagesQuery request, CancellationToken cancellationToken)
    {
        var userId = _user.Id; // دریافت شناسه کاربر فعلی

        var messagesWithStatus = await _context.ChatMessages
            .Where(m => m.ChatRoomId == request.ChatRoomId && !m.IsDeleted)
            .OrderByDescending(m => m.Created)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(m => new
            {
                Message = m,
                DeliveryStatus =
                    m.SenderId == userId
                        ? (
                            m.Statuses.Any(s => s.UserId != userId && s.Status == ReadStatus.Read) ? ReadStatus.Read :
                            (m.Statuses.Any(s => s.UserId != userId && s.Status >= ReadStatus.Delivered) ? ReadStatus.Delivered : ReadStatus.Sent)
                          )
                        : (
                            // برای پیام‌های دریافتی از دیگران، وضعیت برای فرستنده نمایش داده نمی‌شود
                            // اما برای سازگاری مقدار Sent برمی‌گردانیم
                            ReadStatus.Sent
                          )
            })
            .ToListAsync(cancellationToken);

        var dtos = messagesWithStatus
            .Select(x =>
            {
                var dto = _mapper.Map<ChatMessageDto>(x.Message);
                dto.DeliveryStatus = x.DeliveryStatus;
                // Timestamp از مپینگ به UTC ست می‌شود
                return dto;
            })
            .OrderBy(m => m.Timestamp)
            .ToList();

        return dtos;
    }
}
