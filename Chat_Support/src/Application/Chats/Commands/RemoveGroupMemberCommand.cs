using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Domain.Entities;
using Chat_Support.Domain.Enums;

namespace Chat_Support.Application.Chats.Commands;

public record RemoveGroupMemberCommand(
    int ChatRoomId,
    int UserId
) : IRequest<bool>;

public class RemoveGroupMemberCommandHandler : IRequestHandler<RemoveGroupMemberCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IChatHubService _chatHubService;

    public RemoveGroupMemberCommandHandler(
        IApplicationDbContext context,
        IUser user,
        IChatHubService chatHubService)
    {
        _context = context;
        _user = user;
        _chatHubService = chatHubService;
    }

    public async Task<bool> Handle(RemoveGroupMemberCommand request, CancellationToken cancellationToken)
    {
        // Check permissions
        var requesterMember = await _context.ChatRoomMembers
            .FirstOrDefaultAsync(m => m.ChatRoomId == request.ChatRoomId
                && m.UserId == _user.Id
                && (m.Role == ChatRole.Admin || m.Role == ChatRole.Owner),
                cancellationToken);

        if (requesterMember == null)
        {
            // Check if user is removing themselves
            if (request.UserId != _user.Id)
                throw new UnauthorizedAccessException("فقط مدیران میتوانند که کاربران را حذف کنند");
        }

        var memberToRemove = await _context.ChatRoomMembers
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.ChatRoomId == request.ChatRoomId && m.UserId == request.UserId,
                cancellationToken);

        if (memberToRemove == null)
            return false;

        // Can't remove owner
        if (memberToRemove.Role == ChatRole.Owner && request.UserId != _user.Id)
            throw new InvalidOperationException("مالک گروه را نمیتوان حذف کرد");

        _context.ChatRoomMembers.Remove(memberToRemove);

        // System message
        var systemMessage = new ChatMessage
        {
            Content = request.UserId == _user.Id
                ? $"{memberToRemove.User.FirstName} {memberToRemove.User.LastName} گروه را ترک کرد"
                : $"{memberToRemove.User.FirstName} {memberToRemove.User.LastName} از گروه حذف شد",
            ChatRoomId = request.ChatRoomId,
            Type = MessageType.System
        };
        _context.ChatMessages.Add(systemMessage);

        await _context.SaveChangesAsync(cancellationToken);

        // Notify removed user
        await _chatHubService.SendMessageUpdateToRoom(
            request.ChatRoomId.ToString(),
            new { Action = "UserRemoved", UserId = request.UserId },
            "GroupMemberRemoved");

        return true;
    }
}
