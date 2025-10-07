using Chat_Support.Application.Common.Interfaces;

namespace Chat_Support.Application.Chats.Commands;

public record ToggleChatRoomMuteCommand(int RoomId, int UserId, bool IsMuted) : IRequest<bool>;

public class ToggleChatRoomMuteCommandHandler : IRequestHandler<ToggleChatRoomMuteCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public ToggleChatRoomMuteCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(ToggleChatRoomMuteCommand request, CancellationToken cancellationToken)
    {
        var member = await _context.ChatRoomMembers
            .FirstOrDefaultAsync(m => m.ChatRoomId == request.RoomId && m.UserId == request.UserId, cancellationToken);

        if (member == null)
            return false;

        member.IsMuted = request.IsMuted;
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
