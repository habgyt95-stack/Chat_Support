using Chat_Support.Application.Admin.DTOs;
using Chat_Support.Application.Chats.DTOs;
using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Application.Common.Models;
using Chat_Support.Application.Common.Exceptions;
using NotFoundException = Chat_Support.Application.Common.Exceptions.NotFoundException;

namespace Chat_Support.Application.Admin.Queries.GetChatMessagesForAdmin;

/// <summary>
/// Query برای دریافت پیام‌های یک چت خاص توسط ادمین
/// </summary>
public record GetChatMessagesForAdminQuery : IRequest<PaginatedList<AdminChatMessageDto>>
{
    public int ChatRoomId { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    
    // فیلتر جستجو در محتوای پیام
    public string? SearchTerm { get; init; }
    
    // فیلتر بر اساس فرستنده
    public int? SenderId { get; init; }
    
    // فیلتر بر اساس بازه زمانی
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }

    // فیلتر وضعیت حذف: null = پیش‌فرض (فقط نمایش پیام‌های حذف‌نشده)
    // true = فقط پیام‌های حذف‌شده؛ false = فقط پیام‌های حذف‌نشده
    public bool? IsDeleted { get; init; }
}

public class GetChatMessagesForAdminQueryHandler 
    : IRequestHandler<GetChatMessagesForAdminQuery, PaginatedList<AdminChatMessageDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IMapper _mapper;

    public GetChatMessagesForAdminQueryHandler(
        IApplicationDbContext context,
        IUser user,
        IMapper mapper)
    {
        _context = context;
        _user = user;
        _mapper = mapper;
    }

    public async Task<PaginatedList<AdminChatMessageDto>> Handle(
        GetChatMessagesForAdminQuery request,
        CancellationToken cancellationToken)
    {
        // بررسی دسترسی: آیا ادمین به این چت دسترسی دارد؟
        var chatRoom = await _context.ChatRooms
            .AsNoTracking()
            .FirstOrDefaultAsync(cr => cr.Id == request.ChatRoomId, cancellationToken);

        if (chatRoom == null)
        {
            throw new NotFoundException(nameof(Domain.Entities.ChatRoom), request.ChatRoomId.ToString());
        }

        // بررسی دسترسی: آیا کاربر یک SupportAgent فعال است؟
        var currentUserAgent = await _context.SupportAgents
            .AsNoTracking()
            .FirstOrDefaultAsync(sa => sa.UserId == _user.Id && sa.IsActive, cancellationToken);
        
        if (currentUserAgent == null)
        {
            throw new ForbiddenAccessException();
        }

        var userRegionId = _user.RegionId;
        var isSystemAdmin = userRegionId <= 0;

        // RegionAdmin فقط چت‌های ناحیه خودش را می‌بیند
        if (!isSystemAdmin && userRegionId > 0 && chatRoom.RegionId != userRegionId)
        {
            throw new ForbiddenAccessException();
        }

        // شروع کوئری
        var query = _context.ChatMessages
            .AsNoTracking()
            .Where(m => m.ChatRoomId == request.ChatRoomId)
            .Include(m => m.Sender)
            .Include(m => m.ReplyToMessage)!.ThenInclude(r => r!.Sender)
            .Include(m => m.Reactions).ThenInclude(r => r.User)
            .Include(m => m.Statuses).ThenInclude(s => s.User)
            .AsQueryable();

        // فیلتر وضعیت حذف - پیش‌فرض فقط پیام‌های حذف‌نشده
        if (request.IsDeleted.HasValue)
        {
            query = query.Where(m => m.IsDeleted == request.IsDeleted.Value);
        }
        else
        {
            query = query.Where(m => !m.IsDeleted);
        }

        // اعمال فیلتر جستجو
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchLower = request.SearchTerm.ToLower();
            query = query.Where(m => m.Content.ToLower().Contains(searchLower));
        }

        // فیلتر فرستنده
        if (request.SenderId.HasValue)
        {
            query = query.Where(m => m.SenderId == request.SenderId.Value);
        }

        // فیلتر بازه زمانی
        if (request.FromDate.HasValue)
        {
            var fromUtc = request.FromDate.Value.ToUniversalTime();
            query = query.Where(m => m.Created >= fromUtc);
        }
        if (request.ToDate.HasValue)
        {
            var toUtc = request.ToDate.Value.ToUniversalTime();
            query = query.Where(m => m.Created <= toUtc);
        }

        // مرتب‌سازی بر اساس تاریخ (قدیمی‌ترین اول برای نمایش طبیعی گفتگو)
        query = query.OrderBy(m => m.Created);

        // ایجاد PaginatedList
        var totalCount = await query.CountAsync(cancellationToken);
        var messages = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = _mapper.Map<List<AdminChatMessageDto>>(messages);

        // پر کردن واکنش‌ها به صورت دستی (گروه‌بندی بر اساس اموجی)
        for (int i = 0; i < messages.Count; i++)
        {
            var message = messages[i];
            var dto = dtos[i];

            var grouped = message.Reactions
                .GroupBy(r => r.Emoji)
                .Select(g => new ReactionInfo
                {
                    Emoji = g.Key,
                    Count = g.Count(),
                    IsReactedByCurrentUser = g.Any(r => r.UserId == _user.Id),
                    UserFullNames = g
                        .Select(r => (r.User != null ? ($"{r.User.FirstName} {r.User.LastName}").Trim() : "نامشخص"))
                        .ToList()
                })
                .ToList();

            dto.Reactions = grouped;
        }

        return new PaginatedList<AdminChatMessageDto>(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}
