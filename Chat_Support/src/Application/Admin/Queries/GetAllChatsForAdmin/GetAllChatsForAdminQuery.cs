using Chat_Support.Application.Admin.DTOs;
using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Application.Common.Models;
using Chat_Support.Application.Common.Exceptions;
using Chat_Support.Domain.Enums;

namespace Chat_Support.Application.Admin.Queries.GetAllChatsForAdmin;

/// <summary>
/// Query برای دریافت تمام چت‌ها با فیلترهای قوی برای داشبورد ادمین
/// </summary>
public record GetAllChatsForAdminQuery : IRequest<PaginatedList<AdminChatRoomDto>>
{
    // فیلترهای جستجو
    public string? SearchTerm { get; init; }
    
    // فیلتر بر اساس نوع چت
    public ChatRoomType? ChatRoomType { get; init; }
    
    // فیلتر بر اساس ناحیه (اگر null باشد، همه نواحی)
    public int? RegionId { get; init; }
    
    // فیلتر بر اساس بازه زمانی ایجاد
    public DateTime? CreatedFrom { get; init; }
    public DateTime? CreatedTo { get; init; }
    
    // فیلتر چت‌های حذف شده
    public bool? IsDeleted { get; init; }
    
    // فیلتر چت‌های گروهی
    public bool? IsGroup { get; init; }
    
    // فیلتر بر اساس تعداد اعضا
    public int? MinMembersCount { get; init; }
    public int? MaxMembersCount { get; init; }
    
    // فیلتر بر اساس تعداد پیام‌ها
    public int? MinMessagesCount { get; init; }
    public int? MaxMessagesCount { get; init; }
    
    // فیلتر بر اساس فعالیت اخیر
    public DateTime? LastActivityFrom { get; init; }
    public DateTime? LastActivityTo { get; init; }
    
    // مرتب‌سازی
    public string? SortBy { get; init; } // "CreatedAt", "LastActivity", "MessagesCount", "MembersCount", "Name"
    public bool IsDescending { get; init; } = true;
    
    // صفحه‌بندی
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public class GetAllChatsForAdminQueryHandler : IRequestHandler<GetAllChatsForAdminQuery, PaginatedList<AdminChatRoomDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IMapper _mapper;

    public GetAllChatsForAdminQueryHandler(
        IApplicationDbContext context,
        IUser user,
        IMapper mapper)
    {
        _context = context;
        _user = user;
        _mapper = mapper;
    }

    public async Task<PaginatedList<AdminChatRoomDto>> Handle(
        GetAllChatsForAdminQuery request,
        CancellationToken cancellationToken)
    {
        // بررسی دسترسی: آیا کاربر یک SupportAgent فعال است؟
        var currentUserAgent = await _context.SupportAgents
            .AsNoTracking()
            .FirstOrDefaultAsync(sa => sa.UserId == _user.Id && sa.IsActive, cancellationToken);
        
        if (currentUserAgent == null)
        {
            throw new ForbiddenAccessException();
        }

        // شروع کوئری پایه
        var query = _context.ChatRooms
            .AsNoTracking()
            .Include(cr => cr.Region)
            .Include(cr => cr.CreatedBy)
            .Include(cr => cr.Members).ThenInclude(m => m.User)
            .Include(cr => cr.Messages.OrderByDescending(m => m.Created).Take(1))
                .ThenInclude(m => m.Sender)
            .AsQueryable();

        // بررسی نقش کاربر: آیا به همه نواحی دسترسی دارد؟
        var userRegionId = _user.RegionId;
        var isSystemAdmin = userRegionId <= 0;

        if (!isSystemAdmin && userRegionId > 0)
        {
            // RegionAdmin فقط چت‌های ناحیه خودش را می‌بیند
            query = query.Where(cr => cr.RegionId == userRegionId);
        }

        // اعمال فیلتر جستجو (نام چت، توضیحات، شماره تلفن اعضا)
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchLower = request.SearchTerm.ToLower();
            query = query.Where(cr =>
                cr.Name.ToLower().Contains(searchLower) ||
                (cr.Description != null && cr.Description.ToLower().Contains(searchLower)) ||
                cr.Members.Any(m => m.User.Mobile != null && m.User.Mobile.Contains(request.SearchTerm)) ||
                cr.Members.Any(m => (m.User.FirstName + " " + m.User.LastName).ToLower().Contains(searchLower))
            );
        }

        // فیلتر نوع چت
        if (request.ChatRoomType.HasValue)
        {
            query = query.Where(cr => cr.ChatRoomType == request.ChatRoomType.Value);
        }

        // فیلتر ناحیه (برای System Admin)
        if (request.RegionId.HasValue)
        {
            query = query.Where(cr => cr.RegionId == request.RegionId.Value);
        }

        // فیلتر بازه زمانی ایجاد
        if (request.CreatedFrom.HasValue)
        {
            var createdFromUtc = request.CreatedFrom.Value.ToUniversalTime();
            query = query.Where(cr => cr.Created >= createdFromUtc);
        }
        if (request.CreatedTo.HasValue)
        {
            var createdToUtc = request.CreatedTo.Value.ToUniversalTime();
            query = query.Where(cr => cr.Created <= createdToUtc);
        }

        // فیلتر حذف شده‌ها
        if (request.IsDeleted.HasValue)
        {
            query = query.Where(cr => cr.IsDeleted == request.IsDeleted.Value);
        }
        else
        {
            // به صورت پیش‌فرض چت‌های حذف شده نمایش داده نمی‌شوند
            query = query.Where(cr => !cr.IsDeleted);
        }

        // فیلتر گروهی بودن
        if (request.IsGroup.HasValue)
        {
            query = query.Where(cr => cr.IsGroup == request.IsGroup.Value);
        }

        // فیلتر تعداد اعضا
        if (request.MinMembersCount.HasValue)
        {
            query = query.Where(cr => cr.Members.Count >= request.MinMembersCount.Value);
        }
        if (request.MaxMembersCount.HasValue)
        {
            query = query.Where(cr => cr.Members.Count <= request.MaxMembersCount.Value);
        }

        // برای فیلترهای مربوط به تعداد پیام و آخرین فعالیت، باید در حافظه پردازش شوند
        // زیرا این فیلترها به محاسبات پیچیده‌تر نیاز دارند
        
        // مرتب‌سازی
        query = ApplySorting(query, request.SortBy, request.IsDescending);

        // اجرای کوئری و دریافت نتایج
        var totalCount = await query.CountAsync(cancellationToken);
        
        var chatRooms = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        // تبدیل به DTO و افزودن اطلاعات اضافی
        var dtos = new List<AdminChatRoomDto>();
        foreach (var room in chatRooms)
        {
            var dto = _mapper.Map<AdminChatRoomDto>(room);
            
            // محاسبه تعداد کل پیام‌ها
            dto.TotalMessagesCount = await _context.ChatMessages
                .CountAsync(m => m.ChatRoomId == room.Id && !m.IsDeleted, cancellationToken);
            
            // اطلاعات آخرین پیام
            var lastMessage = room.Messages.OrderByDescending(m => m.Created).FirstOrDefault();
            if (lastMessage != null)
            {
                dto.LastMessageTime = lastMessage.Created.UtcDateTime;
                dto.LastMessageContent = lastMessage.Content;
                dto.LastMessageSenderName = lastMessage.Sender != null
                    ? $"{lastMessage.Sender.FirstName} {lastMessage.Sender.LastName}"
                    : "مهمان";
            }
            
            // اطلاعات اعضا
            dto.Members = _mapper.Map<List<AdminChatRoomMemberDto>>(room.Members);
            
            dtos.Add(dto);
        }

        // اعمال فیلترهای مربوط به تعداد پیام (در حافظه)
        if (request.MinMessagesCount.HasValue)
        {
            dtos = dtos.Where(d => d.TotalMessagesCount >= request.MinMessagesCount.Value).ToList();
        }
        if (request.MaxMessagesCount.HasValue)
        {
            dtos = dtos.Where(d => d.TotalMessagesCount <= request.MaxMessagesCount.Value).ToList();
        }

        // اعمال فیلترهای مربوط به آخرین فعالیت (در حافظه)
        if (request.LastActivityFrom.HasValue)
        {
            dtos = dtos.Where(d => d.LastMessageTime >= request.LastActivityFrom.Value).ToList();
        }
        if (request.LastActivityTo.HasValue)
        {
            dtos = dtos.Where(d => d.LastMessageTime <= request.LastActivityTo.Value).ToList();
        }

        // ایجاد PaginatedList
        return new PaginatedList<AdminChatRoomDto>(dtos, totalCount, request.PageNumber, request.PageSize);
    }

    private static IQueryable<Domain.Entities.ChatRoom> ApplySorting(
        IQueryable<Domain.Entities.ChatRoom> query,
        string? sortBy,
        bool isDescending)
    {
        return sortBy?.ToLower() switch
        {
            "name" => isDescending
                ? query.OrderByDescending(cr => cr.Name)
                : query.OrderBy(cr => cr.Name),
            
            "createdat" => isDescending
                ? query.OrderByDescending(cr => cr.Created)
                : query.OrderBy(cr => cr.Created),
            
            "memberscount" => isDescending
                ? query.OrderByDescending(cr => cr.Members.Count)
                : query.OrderBy(cr => cr.Members.Count),
            
            // مرتب‌سازی پیش‌فرض بر اساس تاریخ ایجاد (جدیدترین)
            _ => query.OrderByDescending(cr => cr.Created)
        };
    }
}
