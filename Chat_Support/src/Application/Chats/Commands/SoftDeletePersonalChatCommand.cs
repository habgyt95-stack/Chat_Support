using Chat_Support.Application.Common.Interfaces;

namespace Chat_Support.Application.Chats.Commands;

public record SoftDeletePersonalChatCommand(int ChatRoomId) : IRequest<bool>;

public class SoftDeletePersonalChatCommandHandler : IRequestHandler<SoftDeletePersonalChatCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IChatHubService _chatHubService;

    public SoftDeletePersonalChatCommandHandler(
        IApplicationDbContext context,
        IUser user,
        IChatHubService chatHubService)
    {
        _context = context;
        _user = user;
        _chatHubService = chatHubService;
    }

    public async Task<bool> Handle(SoftDeletePersonalChatCommand request, CancellationToken cancellationToken)
    {
        var chatRoom = await _context.ChatRooms
            .Include(cr => cr.Members)
            .FirstOrDefaultAsync(cr => cr.Id == request.ChatRoomId && !cr.IsGroup, cancellationToken);

        if (chatRoom == null)
            return false;

        // Find current user's membership
        var userMembership = chatRoom.Members.FirstOrDefault(m => m.UserId == _user.Id);
        if (userMembership == null)
            throw new UnauthorizedAccessException("You are not a member of this chat");

        // Soft delete - just mark as deleted
        userMembership.IsDeleted = true;

        await _context.SaveChangesAsync(cancellationToken);

        // Notify user that chat was deleted
        await _chatHubService.SendMessageUpdateToRoom(
            request.ChatRoomId.ToString(),
            new { Action = "ChatDeleted", UserId = _user.Id, ChatRoomId = request.ChatRoomId },
            "PersonalChatDeleted");

        return true;
    }
}
