using Chat_Support.Application.Admin.Queries.GetAdminChatStats;
using Chat_Support.Application.Admin.Queries.GetAllChatsForAdmin;
using Chat_Support.Application.Admin.Queries.GetChatMessagesForAdmin;
using Chat_Support.Domain.Enums;

namespace Chat_Support.Web.Endpoints;

public class AdminChat : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var adminChatApi = app.MapGroup("/api/admin/chats")
            .RequireAuthorization(); // تمام endpoint های این گروه نیاز به احراز هویت دارند

        // دریافت لیست تمام چت‌ها با فیلترهای قوی
        adminChatApi.MapGet("/", GetAllChats)
            .WithName("GetAllChatsForAdmin")
            .WithOpenApi();

        // دریافت آمار کلی
        adminChatApi.MapGet("/stats", GetChatStats)
            .WithName("GetAdminChatStats")
            .WithOpenApi();

        // دریافت پیام‌های یک چت خاص
        adminChatApi.MapGet("/{chatRoomId:int}/messages", GetChatMessages)
            .WithName("GetChatMessagesForAdmin")
            .WithOpenApi();
    }

    private async Task<IResult> GetAllChats(
        ISender sender,
        [AsParameters] GetAllChatsForAdminQuery query)
    {
        var result = await sender.Send(query);
        return Results.Ok(result);
    }

    private async Task<IResult> GetChatStats(ISender sender)
    {
        var result = await sender.Send(new GetAdminChatStatsQuery());
        return Results.Ok(result);
    }

    private async Task<IResult> GetChatMessages(
        ISender sender,
        int chatRoomId,
        int pageNumber = 1,
        int pageSize = 50,
        string? searchTerm = null,
        int? senderId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        var query = new GetChatMessagesForAdminQuery
        {
            ChatRoomId = chatRoomId,
            PageNumber = pageNumber,
            PageSize = pageSize,
            SearchTerm = searchTerm,
            SenderId = senderId,
            FromDate = fromDate,
            ToDate = toDate
        };

        var result = await sender.Send(query);
        return Results.Ok(result);
    }
}
