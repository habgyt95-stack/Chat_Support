using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Domain.Enums;

namespace Chat_Support.Application.Chats.Commands;

public record DeleteChatRoomCommand(int ChatRoomId) : IRequest<bool>;

public class DeleteChatRoomCommandHandler : IRequestHandler<DeleteChatRoomCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public DeleteChatRoomCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<bool> Handle(DeleteChatRoomCommand request, CancellationToken cancellationToken)
    {
        var chatRoom = await _context.ChatRooms
            .Include(cr => cr.Members)
            .FirstOrDefaultAsync(cr => cr.Id == request.ChatRoomId, cancellationToken);

        if (chatRoom == null)
            return false;

        // Check if user is owner or it's a personal chat
        if (chatRoom.IsGroup)
        {
            var isOwner = chatRoom.Members.Any(m => m.UserId == _user.Id && m.Role == ChatRole.Owner);
            if (!isOwner)
                throw new UnauthorizedAccessException("Only group owner can delete the group");
        }
        else
        {
            // For personal chats, just remove the member
            var member = chatRoom.Members.FirstOrDefault(m => m.UserId == _user.Id);
            if (member != null)
            {
                _context.ChatRoomMembers.Remove(member);
                await _context.SaveChangesAsync(cancellationToken);
                return true;
            }
        }

        // Delete entire room if group
        _context.ChatRooms.Remove(chatRoom);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
