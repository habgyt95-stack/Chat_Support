using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Application.Common.Exceptions;

namespace Chat_Support.Application.Admin.Queries.GetAdminChatStats;

/// <summary>
/// آمار کلی چت‌ها برای نمایش در داشبورد ادمین
/// </summary>
public class AdminChatStatsDto
{
    public int TotalChats { get; set; }
    public int ActiveChats { get; set; }
    public int GroupChats { get; set; }
    public int PersonalChats { get; set; }
    public int SupportChats { get; set; }
    public int TotalMessages { get; set; }
    public int TotalUsers { get; set; }
    public int TodayMessages { get; set; }
    public int TodayNewChats { get; set; }
    public double AverageMessagesPerChat { get; set; }
    public double AverageMembersPerGroup { get; set; }
    
    // آمار به تفکیک ناحیه (برای System Admin)
    public List<RegionStatsDto> RegionStats { get; set; } = new();
}

public class RegionStatsDto
{
    public int RegionId { get; set; }
    public string RegionName { get; set; } = string.Empty;
    public int ChatsCount { get; set; }
    public int MessagesCount { get; set; }
    public int UsersCount { get; set; }
}

public record GetAdminChatStatsQuery : IRequest<AdminChatStatsDto>;

public class GetAdminChatStatsQueryHandler : IRequestHandler<GetAdminChatStatsQuery, AdminChatStatsDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public GetAdminChatStatsQueryHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<AdminChatStatsDto> Handle(GetAdminChatStatsQuery request, CancellationToken cancellationToken)
    {
        // بررسی دسترسی: آیا کاربر یک SupportAgent فعال است؟
        var currentUserAgent = await _context.SupportAgents
            .AsNoTracking()
            .FirstOrDefaultAsync(sa => sa.UserId == _user.Id && sa.IsActive, cancellationToken);
        
        if (currentUserAgent == null)
        {
            throw new ForbiddenAccessException();
        }
        
        var userRegionId = _user.RegionId;
        // برای سادگی: اگر RegionId = 0 یا منفی باشد، به همه نواحی دسترسی دارد (System Admin)
        var isSystemAdmin = userRegionId <= 0;

        var stats = new AdminChatStatsDto();
        
        // کوئری پایه چت‌ها (بر اساس دسترسی)
        var chatsQuery = _context.ChatRooms.AsNoTracking();
        if (!isSystemAdmin && userRegionId > 0)
        {
            chatsQuery = chatsQuery.Where(cr => cr.RegionId == userRegionId);
        }
        
        var activeChatsQuery = chatsQuery.Where(cr => !cr.IsDeleted);

        // تعداد کل چت‌ها
        stats.TotalChats = await activeChatsQuery.CountAsync(cancellationToken);
        
        // تعداد چت‌های فعال (که حداقل یک پیام در ۷ روز گذشته داشته‌اند)
        var sevenDaysAgo = DateTimeOffset.UtcNow.AddDays(-7);
        stats.ActiveChats = await activeChatsQuery
            .Where(cr => cr.Messages.Any(m => m.Created >= sevenDaysAgo && !m.IsDeleted))
            .CountAsync(cancellationToken);
        
        // تفکیک بر اساس نوع چت
        stats.GroupChats = await activeChatsQuery
            .Where(cr => cr.ChatRoomType == Domain.Enums.ChatRoomType.Group)
            .CountAsync(cancellationToken);
        
        stats.PersonalChats = await activeChatsQuery
            .Where(cr => cr.ChatRoomType == Domain.Enums.ChatRoomType.UserToUser)
            .CountAsync(cancellationToken);
        
        stats.SupportChats = await activeChatsQuery
            .Where(cr => cr.ChatRoomType == Domain.Enums.ChatRoomType.Support)
            .CountAsync(cancellationToken);
        
        // کوئری پایه پیام‌ها
        var messagesQuery = _context.ChatMessages
            .AsNoTracking()
            .Where(m => !m.IsDeleted);
        
        if (!isSystemAdmin && userRegionId > 0)
        {
            messagesQuery = messagesQuery.Where(m => m.ChatRoom.RegionId == userRegionId);
        }
        
        // تعداد کل پیام‌ها
        stats.TotalMessages = await messagesQuery.CountAsync(cancellationToken);
        
        // پیام‌های امروز
        var todayStart = DateTimeOffset.UtcNow.Date;
        stats.TodayMessages = await messagesQuery
            .Where(m => m.Created >= todayStart)
            .CountAsync(cancellationToken);
        
        // چت‌های جدید امروز
        stats.TodayNewChats = await activeChatsQuery
            .Where(cr => cr.Created >= todayStart)
            .CountAsync(cancellationToken);
        
        // میانگین پیام به ازای هر چت
        if (stats.TotalChats > 0)
        {
            stats.AverageMessagesPerChat = Math.Round((double)stats.TotalMessages / stats.TotalChats, 2);
        }
        
        // میانگین اعضا به ازای هر گروه
        var groupMembersCount = await activeChatsQuery
            .Where(cr => cr.IsGroup)
            .Select(cr => cr.Members.Count)
            .ToListAsync(cancellationToken);
        
        if (groupMembersCount.Any())
        {
            stats.AverageMembersPerGroup = Math.Round(groupMembersCount.Average(), 2);
        }
        
        // تعداد کل کاربران
        var usersQuery = _context.KciUsers.AsNoTracking();
        if (!isSystemAdmin && userRegionId > 0)
        {
            usersQuery = usersQuery.Where(u => u.RegionId == userRegionId);
        }
        stats.TotalUsers = await usersQuery.CountAsync(cancellationToken);
        
        // آمار به تفکیک ناحیه (فقط برای System Admin)
        if (isSystemAdmin)
        {
            var regionStats = await _context.Regions
                .AsNoTracking()
                .Select(r => new RegionStatsDto
                {
                    RegionId = r.Id,
                    RegionName = r.Name,
                    ChatsCount = r.ChatRooms.Count(cr => !cr.IsDeleted),
                    MessagesCount = r.ChatRooms
                        .SelectMany(cr => cr.Messages)
                        .Count(m => !m.IsDeleted),
                    UsersCount = r.Users.Count()
                })
                .Where(rs => rs.ChatsCount > 0 || rs.UsersCount > 0)
                .ToListAsync(cancellationToken);
            
            stats.RegionStats = regionStats;
        }

        return stats;
    }
}
