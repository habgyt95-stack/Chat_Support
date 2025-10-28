using AutoMapper;
using Chat_Support.Application.Chats.Commands;
using Chat_Support.Application.Chats.DTOs;
using Chat_Support.Application.Chats.Queries;
using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Domain.Entities;
using Chat_Support.Domain.Enums;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chat_Support.Web.Endpoints;

public class Chat : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var chatApi = app.MapGroup("/api/chat"); // حذف RequireAuthorization پیش‌فرض

        // Chat Room endpoints
        chatApi.MapGet("/rooms", GetChatRooms).RequireAuthorization();
        chatApi.MapGet("/rooms/{roomId:int}/messages", GetChatMessages)
            .AllowAnonymous()
            .RequireCors("ChatSupportApp"); // مهمان هم بتواند پیام‌ها را بگیرد
        chatApi.MapPost("/rooms", CreateChatRoom).RequireAuthorization();
        chatApi.MapPost("/rooms/{roomId:int}/join", JoinChatRoom).RequireAuthorization();
        chatApi.MapDelete("/rooms/{roomId:int}/leave", LeaveChatRoom).RequireAuthorization();
        chatApi.MapGet("/rooms/{roomId:int}/members", GetChatRoomMembers).RequireAuthorization();
        chatApi.MapGet("/rooms/{roomId}/unread-count", GetUnreadCount).RequireAuthorization();
        // Message endpoints
        chatApi.MapPost("/rooms/{roomId:int}/messages", SendMessage)
            .AllowAnonymous()
            .RequireCors("ChatSupportApp"); // مهمان بتواند پیام ارسال کند
        chatApi.MapPut("/messages/{messageId:int}", EditMessage).WithName("EditChatMessage").RequireAuthorization();
        chatApi.MapDelete("/messages/{messageId:int}", DeleteMessage).WithName("DeleteChatMessage").RequireAuthorization();
        chatApi.MapPost("/messages/{messageId:int}/react", ReactToMessage).WithName("ReactToChatMessage").RequireAuthorization();
        chatApi.MapPost("/messages/forward", ForwardMessage).WithName("ForwardChatMessage").RequireAuthorization();
        chatApi.MapGet("/messages/{messageId:int}/read-receipts", GetMessageReadReceipts).RequireAuthorization();

        // User endpoints
        chatApi.MapGet("/users/online", GetOnlineUsers).RequireAuthorization();
        chatApi.MapGet("/users/search", SearchUsers).RequireAuthorization();
    // Global search (users + messages)
    chatApi.MapGet("/search", SearchEverything).RequireAuthorization();

        // File upload endpoint
        chatApi.MapPost("/upload", UploadFile)
            .AllowAnonymous()
            .RequireCors("ChatSupportApp")
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data");

        // File download endpoint
        chatApi.MapGet("/download", DownloadFile)
            .AllowAnonymous()
            .RequireCors("ChatSupportApp");

        // File meta endpoint (return original file name/size/type by stored metadata)
        chatApi.MapGet("/file-meta", GetFileMeta)
            .AllowAnonymous()
            .RequireCors("ChatSupportApp");

        // Message context for jumping to a specific message
        chatApi.MapGet("/messages/{messageId:int}/context", GetMessageContext)
            .RequireAuthorization();

        // Group management endpoints
        chatApi.MapPut("/rooms/{roomId:int}", UpdateChatRoom).RequireAuthorization();
        chatApi.MapPost("/rooms/{roomId:int}/members/add", AddGroupMember).RequireAuthorization();
        chatApi.MapDelete("/rooms/{roomId:int}/members/{userId}", RemoveGroupMember).RequireAuthorization();
        chatApi.MapDelete("/rooms/{roomId:int}", DeleteChatRoom).RequireAuthorization();
        chatApi.MapPut("/rooms/{roomId:int}/soft-delete", SoftDeletePersonalChat).RequireAuthorization();
        chatApi.MapPut("/rooms/{roomId:int}/mute", ToggleChatRoomMute).RequireAuthorization();
    }

    [IgnoreAntiforgeryToken]
    public async Task<Results<Ok<ChatFileUploadResult>, BadRequest<string>>> UploadFile(
        ISender sender,
        IFormFile file,
        [FromForm] int chatRoomId,
        [FromForm] MessageType type,
        HttpContext httpContext)
    {
        if (file.Length == 0)
            return TypedResults.BadRequest("No file was uploaded");

        var command = new UploadChatFileCommand
        {
            ChatRoomId = chatRoomId,
            File = file,
            Type = type
        };

        var result = await sender.Send(command);
        if (!result.Succeeded)
            return TypedResults.BadRequest(result.Error ?? "Upload failed");

        return TypedResults.Ok(result.Data!);
    }

    private static async Task<IResult> GetUnreadCount(
        int roomId,
        IApplicationDbContext context,
        IUser user)
    {
        var userId = user.Id;

        var lastReadMessage = await context.ChatRoomMembers
            .Where(m => m.UserId == userId && m.ChatRoomId == roomId)
            .Select(m => m.LastReadMessageId)
            .FirstOrDefaultAsync();

        var unreadCount = await context.ChatMessages
            .Where(m => m.ChatRoomId == roomId &&
                        m.SenderId != userId &&
                        !m.IsDeleted &&
                        (lastReadMessage == null || m.Id > lastReadMessage))
            .CountAsync();

        return Results.Ok(new { UnreadCount = unreadCount });
    }

    private static async Task<IResult> GetChatRooms(
    IApplicationDbContext context,
    IUser user,
    IMapper mapper,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20)
    {
        var userId = user.Id;
        if (string.IsNullOrEmpty(userId.ToString()))
        {
            return Results.Unauthorized();
        }

        // ۲. کوئری شما بسیار قدرتمند است و بخش زیادی از آن را نگه می‌داریم
        // این کوئری با استفاده از select new به یک نوع ناشناس، از مشکل N+1 جلوگیری می‌کند
        var query = context.ChatRooms
            .Where(cr => cr.Members.Any(m => m.UserId == userId && !m.IsDeleted))
            .Select(room => new
            {
                // تمام اطلاعات مورد نیاز را در یک آبجکت ناشناس جمع‌آوری می‌کنیم
                RoomEntity = room,
                CurrentUserMembership = room.Members.FirstOrDefault(m => m.UserId == userId),
                OtherUser = !room.IsGroup ? room.Members.FirstOrDefault(m => m.UserId != userId)!.User : null,
                LastMessage = room.Messages
                    .Where(m => !m.IsDeleted)
                    .OrderByDescending(m => m.Created)
                    .FirstOrDefault()
            });

        // ۳. اجرای کوئری و دریافت نتایج
        var intermediateResults = await query
            .OrderByDescending(x => x.LastMessage != null ? x.LastMessage.Created : x.RoomEntity.Created)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // ۴. تبدیل نتایج میانی به DTO نهایی با کمک AutoMapper
        var finalDtoList = intermediateResults.Select(item =>
        {
            // ابتدا مپینگ پایه را با AutoMapper انجام می‌دهیم
            var dto = mapper.Map<ChatRoomDto>(item.RoomEntity);

            // سپس اطلاعات محاسبه‌شده و سفارشی را روی DTO تنظیم می‌کنیم
            dto.LastMessageContent = item.LastMessage?.Content;
            dto.LastMessageTime = item.LastMessage?.Created.UtcDateTime;
            dto.LastMessageSenderName = item.LastMessage?.Sender != null ? $"{item.LastMessage.Sender.FirstName} {item.LastMessage.Sender.LastName}" : null;

            dto.UnreadCount = context.ChatMessages.Count(m =>
                m.ChatRoomId == item.RoomEntity.Id &&
                m.SenderId != user.Id &&
                !m.IsDeleted &&
                (item.CurrentUserMembership!.LastReadMessageId == null || m.Id > item.CurrentUserMembership.LastReadMessageId)
            );

            // فقط برای گروه‌ها امکان Mute را فعال و مقداردهی می‌کنیم
            var isGroup = item.RoomEntity.IsGroup || item.RoomEntity.ChatRoomType == ChatRoomType.Group;
            dto.IsMuted = isGroup && (item.CurrentUserMembership?.IsMuted ?? false);

            // سفارشی‌سازی نام و آواتار برای چت‌های خصوصی
            if (!dto.IsGroup && item.OtherUser != null)
            {
                dto.Name = $"{item.OtherUser.FirstName} {item.OtherUser.LastName}";
                dto.Avatar = item.OtherUser.ImageName;
            }

            return dto;
        }).ToList();

        return Results.Ok(finalDtoList);
    }

    private static async Task<IResult> GetChatMessages(
        int roomId,
        int page,
        int pageSize,
        IMediator mediator)
    {
        var query = new GetChatMessagesQuery(roomId, page, pageSize);
        var result = await mediator.Send(query);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateChatRoom(
        CreateChatRoomRequest request,
        IMediator mediator)
    {
        var command = new CreateChatRoomCommand(
            request.Name,
            request.Description,
            request.IsGroup,
            request.MemberIds,
            request.RegionId
        );
        var result = await mediator.Send(command);
        return Results.Created($"/api/chat/rooms/{result.Id}", result);
    }

    private static async Task<IResult> SendMessage(
        int roomId,
        SendMessageRequest request,
        IMediator mediator,
        HttpContext httpContext)
    {
        // اگر کاربر مهمان است، SessionId را از هدر بخوان
        string? guestSessionId = httpContext.Request.Headers["X-Session-Id"].ToString();
        var command = new SendMessageCommand(
            roomId,
            request.Content,
            request.Type,
            request.AttachmentUrl,
            request.ReplyToMessageId,
            guestSessionId // مقداردهی پارامتر جدید
        );
        var result = await mediator.Send(command);
        return Results.Created($"/api/chat/messages/{result.Id}", result);
    }

    private static async Task<IResult> JoinChatRoom(
        int roomId,
        IApplicationDbContext context,
        HttpContext httpContext,
        IUser user)
    {
        var userId = user.Id;

        var existingMember = await context.ChatRoomMembers
            .FirstOrDefaultAsync(m => m.UserId == userId && m.ChatRoomId == roomId);

        if (existingMember != null)
            return Results.BadRequest("Already a member");

        var member = new ChatRoomMember
        {
            UserId = userId,
            ChatRoomId = roomId,
            Role = ChatRole.Member
        };

        context.ChatRoomMembers.Add(member);
        await context.SaveChangesAsync(CancellationToken.None);

        return Results.Ok();
    }

    private static async Task<IResult> LeaveChatRoom(
        int roomId,
        IApplicationDbContext context,
        HttpContext httpContext,
        IUser user)
    {
        var userId = user.Id;

        var member = await context.ChatRoomMembers
            .FirstOrDefaultAsync(m => m.UserId == userId && m.ChatRoomId == roomId);

        if (member == null)
            return Results.NotFound();

        context.ChatRoomMembers.Remove(member);
        await context.SaveChangesAsync(CancellationToken.None);

        return Results.Ok();
    }

    private static async Task<IResult> GetOnlineUsers(
        IApplicationDbContext context,
        IUser user)
    {
        var currentUserId = user.Id;
        var onlineUsers = await context.UserConnections
            .Where(c => c.IsActive && c.UserId != currentUserId)
            .Include(c => c.User)
            .Select(c => new
            {
                c.User.Id,
                UserFullName = c.User.FirstName + " " + c.User.LastName,
                c.User.ImageName
            })
            .Distinct()
            .ToListAsync();

        return Results.Ok(onlineUsers);
    }

    private static async Task<IResult> SearchUsers(
        string query,
        IApplicationDbContext context,
        IUser currentUser)
    {
        var activeRegionId = currentUser.RegionId;
        var currentUserId = currentUser.Id;

        var users = await context.KciUsers
    .Where(u => u.Id != currentUserId)
    .Where(u => u.RegionId == activeRegionId && u.Enable == true)
    .Where(u => u.UserName!.Contains(query) ||
               u.Email!.Contains(query) ||
               u.Mobile!.Contains(query) ||
               (u.FirstName + " " + u.LastName).Contains(query))
    .Select(u => new
    {
        u.Id,
        UserName = u.Mobile,
        FullName = u.FirstName + " " + u.LastName,
        u.Email,
        u.ImageName
    })
    .Take(20)
    .ToListAsync();

        return Results.Ok(users);
    }

    private static async Task<IResult> SearchEverything(
        string query,
        IApplicationDbContext context,
        IUser currentUser)
    {
        var userId = currentUser.Id;
        var regionId = currentUser.RegionId;

        // Users (reuse logic but with small cap)
        var users = await context.KciUsers
            .Where(u => u.Id != userId)
            .Where(u => u.RegionId == regionId && u.Enable == true)
            .Where(u => u.UserName!.Contains(query) ||
                        u.Email!.Contains(query) ||
                        u.Mobile!.Contains(query) ||
                        (u.FirstName + " " + u.LastName).Contains(query))
            .Select(u => new
            {
                u.Id,
                UserName = u.Mobile,
                FullName = u.FirstName + " " + u.LastName,
                u.Email,
                u.ImageName
            })
            .Take(20)
            .ToListAsync();

        // Messages: only within rooms the current user is a member of
        var myRoomIds = await context.ChatRoomMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.ChatRoomId)
            .ToListAsync();

        // Simple text search on message content; include basic metadata
        var messages = await context.ChatMessages
            .Where(m => myRoomIds.Contains(m.ChatRoomId) && !m.IsDeleted)
            .Where(m => m.Content.Contains(query))
            .Include(m => m.ChatRoom)
            .Include(m => m.Sender)
            .OrderByDescending(m => m.Created)
            .Take(50)
            .Select(m => new
            {
                MessageId = m.Id,
                ChatRoomId = m.ChatRoomId,
                ChatRoomName = m.ChatRoom.IsGroup || m.ChatRoom.ChatRoomType != Domain.Enums.ChatRoomType.UserToUser
                    ? m.ChatRoom.Name
                    : (m.ChatRoom.Members
                        .Where(mm => mm.UserId != userId)
                        .Select(mm => mm.User.FirstName + " " + mm.User.LastName)
                        .FirstOrDefault() ?? m.ChatRoom.Name),
                SenderFullName = m.Sender != null ? (m.Sender.FirstName + " " + m.Sender.LastName) : "مهمان",
                Content = m.Content,
                Type = m.Type,
                Timestamp = m.Created.UtcDateTime
            })
            .ToListAsync();

        return Results.Ok(new { users, messages });
    }

    private static async Task<IResult> GetMessageContext(
        int messageId,
        IApplicationDbContext context,
        IUser currentUser,
        IMapper mapper,
        [FromQuery] int before = 15,
        [FromQuery] int after = 15)
    {
        var userId = currentUser.Id;
        var target = await context.ChatMessages
            .Include(m => m.ChatRoom)
            .FirstOrDefaultAsync(m => m.Id == messageId);
        if (target == null)
            return Results.NotFound();

        // Authorization: ensure user is a member of the room
        var isMember = await context.ChatRoomMembers.AnyAsync(m => m.ChatRoomId == target.ChatRoomId && m.UserId == userId);
        if (!isMember)
            return Results.Forbid();

        // Build before/after windows by Created time
        var beforeList = await context.ChatMessages
            .Where(m => m.ChatRoomId == target.ChatRoomId && !m.IsDeleted && m.Created <= target.Created)
            .Include(m => m.Sender)
            .Include(m => m.ReplyToMessage)!.ThenInclude(r => r!.Sender)
            .Include(m => m.Reactions).ThenInclude(r => r.User)
            .OrderByDescending(m => m.Created)
            .Take(Math.Max(before, 1))
            .ToListAsync();
        beforeList.Reverse(); // chronological

        var afterList = await context.ChatMessages
            .Where(m => m.ChatRoomId == target.ChatRoomId && !m.IsDeleted && m.Created > target.Created)
            .Include(m => m.Sender)
            .Include(m => m.ReplyToMessage)!.ThenInclude(r => r!.Sender)
            .Include(m => m.Reactions).ThenInclude(r => r.User)
            .OrderBy(m => m.Created)
            .Take(Math.Max(after, 1))
            .ToListAsync();

        var combined = beforeList.Concat(afterList).ToList();

        // Map to DTOs and compute DeliveryStatus like GetChatMessages
        var dtos = combined.Select(m => mapper.Map<Chat_Support.Application.Chats.DTOs.ChatMessageDto>(m, opt => opt.Items["currentUserId"] = userId)).ToList();

        // Compute delivery status
        var withStatus = await context.ChatMessages
            .Where(m => combined.Select(x => x.Id).Contains(m.Id))
            .Select(m => new { m.Id, m.SenderId, Statuses = m.Statuses })
            .ToListAsync();

        var statusMap = withStatus.ToDictionary(
            x => x.Id,
            x => (x.SenderId == userId
                ? (x.Statuses.Any(s => s.UserId != userId && s.Status == Domain.Enums.ReadStatus.Read)
                    ? Domain.Enums.ReadStatus.Read
                    : (x.Statuses.Any(s => s.UserId != userId && s.Status >= Domain.Enums.ReadStatus.Delivered)
                        ? Domain.Enums.ReadStatus.Delivered
                        : Domain.Enums.ReadStatus.Sent))
                : Domain.Enums.ReadStatus.Sent)
        );

        dtos.ForEach(d =>
        {
            if (statusMap.TryGetValue(d.Id, out var st))
            {
                d.DeliveryStatus = st;
            }
        });

        return Results.Ok(new { chatRoomId = target.ChatRoomId, messages = dtos, targetMessageId = messageId });
    }

    private static async Task<IResult> GetChatRoomMembers(
        int roomId,
        IApplicationDbContext context)
    {
        var members = await context.ChatRoomMembers
            .Where(m => m.ChatRoomId == roomId)
            .Include(m => m.User)
            .Select(m => new
            {
                m.User.Id,
                m.User.UserName,
                m.User.ImageName,
                m.User.FirstName,
                m.User.LastName,
                m.Role,
                m.JoinedAt,
                m.LastSeenAt
            })
            .ToListAsync();

        return Results.Ok(members);
    }

    private static async Task<IResult> EditMessage(
        int messageId,
        EditMessageRequest request,
        IMediator mediator)
    {
        var command = new EditMessageCommand(messageId, request.NewContent);
        var result = await mediator.Send(command);
        return Results.Ok(result);
    }

    private static async Task<IResult> DeleteMessage(
        int messageId,
        IMediator mediator)
    {
        var command = new DeleteMessageCommand(messageId);
        await mediator.Send(command);
        return Results.Ok(new { MessageId = messageId, Status = "Deleted" });
    }

    private static async Task<IResult> ReactToMessage(
        int messageId,
        ReactRequest requestBody,
        IMediator mediator)
    {
        var command = new ReactToMessageCommand(messageId, requestBody.Emoji);
        var result = await mediator.Send(command);
        return Results.Ok(result);
    }

    private static async Task<IResult> ForwardMessage(
        ForwardMessageRequest requestBody,
        IMediator mediator)
    {
        var command = new ForwardMessageCommand(requestBody.OriginalMessageId, requestBody.TargetChatRoomId);
        var result = await mediator.Send(command);
        return Results.Ok(result);
    }

    private static async Task<IResult> AddGroupMember(
        int roomId,
        AddMembersRequest request,
        IMediator mediator)
    {
        var command = new AddGroupMemberCommand(roomId, request.UserIds);
        var result = await mediator.Send(command);
        return Results.Ok(new { Success = result });
    }

    private static async Task<IResult> RemoveGroupMember(
        int roomId,
        int userId,
        IMediator mediator)
    {
        var command = new RemoveGroupMemberCommand(roomId, userId);
        var result = await mediator.Send(command);
        return Results.Ok(new { Success = result });
    }

    private static async Task<IResult> UpdateChatRoom(
        int roomId,
        UpdateChatRoomRequest request,
        IMediator mediator)
    {
        var command = new UpdateChatRoomCommand(roomId, request.Name, request.Description);
        var result = await mediator.Send(command);
        return Results.Ok(result);
    }

    private static async Task<IResult> DeleteChatRoom(
        int roomId,
        IMediator mediator)
    {
        var command = new DeleteChatRoomCommand(roomId);
        var result = await mediator.Send(command);
        return Results.Ok(new { Success = result });
    }

    private static async Task<IResult> SoftDeletePersonalChat(
        int roomId,
        IMediator mediator)
    {
        var command = new SoftDeletePersonalChatCommand(roomId);
        var result = await mediator.Send(command);
        return Results.Ok(new { Success = result });
    }

    // Request DTO
    public record AddMembersRequest(List<int> UserIds);

    // Request DTOs
    public record CreateChatRoomRequest(
        string Name,
        string? Description,
        bool IsGroup,
        List<int>? MemberIds = null,
        int? RegionId = null
    );

    public record UpdateChatRoomRequest(
        string? Name = null,
        string? Description = null
    );

    public record SendMessageRequest(
        string Content,
        MessageType Type = MessageType.Text,
        string? AttachmentUrl = null,
        int? ReplyToMessageId = null
    );

    public record EditMessageRequest(string NewContent);

    public record ReactRequest(string Emoji);

    public record ForwardMessageRequest(int OriginalMessageId, int TargetChatRoomId);

    private static async Task<IResult> ToggleChatRoomMute(
        int roomId,
        ToggleMuteRequest request,
        IUser user,
        ISender sender)
    {
        var command = new ToggleChatRoomMuteCommand(roomId, user.Id, request.IsMuted);
        var result = await sender.Send(command);

        return result ? Results.Ok(new { success = true, isMuted = request.IsMuted }) : Results.NotFound();
    }

    public record ToggleMuteRequest(bool IsMuted);

    private static async Task<IResult> GetMessageReadReceipts(
        int messageId,
        ISender sender)
    {
        var query = new GetMessageReadReceiptsQuery(messageId);
        var receipts = await sender.Send(query);
        return Results.Ok(receipts);
    }

    private static IResult DownloadFile(
        [FromQuery] string filePath,
        IWebHostEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return Results.BadRequest("File path is required");

        // Remove leading /uploads/ if present to normalize the path
        var normalizedPath = filePath.TrimStart('/');
        if (normalizedPath.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            normalizedPath = normalizedPath.Substring(8); // Remove "uploads/"

        // Prevent directory traversal attacks
        var safePath = normalizedPath.Replace("..", "").Replace("\\", "/");
        
        // Build the full path to the file in the uploads directory
        var fullPath = Path.Combine(environment.WebRootPath, "uploads", safePath);

        if (!File.Exists(fullPath))
            return Results.NotFound("File not found");

        // Extract filename for Content-Disposition header
        var fileName = Path.GetFileName(fullPath);

        // Get the MIME type based on file extension
        var contentType = GetContentType(fileName);

        // Return the file with proper headers for WebView compatibility
        var fileStream = File.OpenRead(fullPath);
        
        // Use Stream result with explicit Content-Disposition header for better WebView support
        return Results.Stream(
            fileStream,
            contentType: contentType,
            fileDownloadName: fileName,
            lastModified: File.GetLastWriteTimeUtc(fullPath),
            enableRangeProcessing: true);
    }

    private static async Task<IResult> GetFileMeta(
        [FromQuery] string filePath,
        IApplicationDbContext context)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return Results.BadRequest("File path is required");

        var meta = await context.ChatFileMetadatas
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.FilePath == filePath);

        if (meta == null)
            return Results.NotFound();

        return Results.Ok(new { fileName = meta.FileName, fileSize = meta.FileSize, contentType = meta.ContentType });
    }

    private static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            // Images
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".ico" => "image/x-icon",
            
            // Videos
            ".mp4" => "video/mp4",
            ".avi" => "video/x-msvideo",
            ".mov" => "video/quicktime",
            ".wmv" => "video/x-ms-wmv",
            ".flv" => "video/x-flv",
            ".webm" => "video/webm",
            
            // Audio
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            ".m4a" => "audio/mp4",
            ".aac" => "audio/aac",
            
            // Documents
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            ".rtf" => "application/rtf",
            
            // Archives
            ".zip" => "application/zip",
            ".rar" => "application/x-rar-compressed",
            ".7z" => "application/x-7z-compressed",
            ".tar" => "application/x-tar",
            ".gz" => "application/gzip",
            
            // Other
            ".json" => "application/json",
            ".xml" => "application/xml",
            
            // Default
            _ => "application/octet-stream"
        };
    }
}
