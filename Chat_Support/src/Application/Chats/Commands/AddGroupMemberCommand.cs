using Chat_Support.Application.Chats.DTOs;
using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Domain.Entities;
using Chat_Support.Domain.Enums;

namespace Chat_Support.Application.Chats.Commands;

public record AddGroupMemberCommand(
    int ChatRoomId,
    List<int> UserIds,
    ChatRole Role = ChatRole.Member
) : IRequest<bool>;

public class AddGroupMemberCommandHandler : IRequestHandler<AddGroupMemberCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IChatHubService _chatHubService;
    private readonly IMapper _mapper;

    public AddGroupMemberCommandHandler(
        IApplicationDbContext context,
        IUser user,
        IChatHubService chatHubService, IMapper mapper)
    {
        _context = context;
        _user = user;
        _chatHubService = chatHubService;
        _mapper = mapper;
    }

    public async Task<bool> Handle(AddGroupMemberCommand request, CancellationToken cancellationToken)
    {
        // Check if requester is admin/owner
        var requesterMember = await _context.ChatRoomMembers
            .FirstOrDefaultAsync(m => m.ChatRoomId == request.ChatRoomId
                && m.UserId == _user.Id
                && (m.Role == ChatRole.Admin || m.Role == ChatRole.Owner),
                cancellationToken);

        if (requesterMember == null)
            throw new UnauthorizedAccessException("Only admins can add members");

        var chatRoom = await _context.ChatRooms
            .Include(cr => cr.Members)
            .FirstOrDefaultAsync(cr => cr.Id == request.ChatRoomId, cancellationToken);

        if (chatRoom == null || !chatRoom.IsGroup)
            return false;

        foreach (var userId in request.UserIds)
        {
            // Check if already member
            if (chatRoom.Members.Any(m => m.UserId == userId))
                continue;

            var member = new ChatRoomMember
            {
                UserId = userId,
                ChatRoomId = request.ChatRoomId,
                Role = request.Role
            };
            _context.ChatRoomMembers.Add(member);

            // System message
            var user = await _context.KciUsers.FindAsync(new object[] { userId }, cancellationToken);
            var systemMessage = new ChatMessage
            {
                Content = $"{user?.FirstName} {user?.LastName} was added to the group",
                ChatRoomId = request.ChatRoomId,
                Type = MessageType.System
            };
            _context.ChatMessages.Add(systemMessage);
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Update room for all members
        var roomDto = await GetUpdatedRoomDto(request.ChatRoomId, cancellationToken);
        foreach (var member in chatRoom.Members)
        {
            if (roomDto != null)
            {
                await _chatHubService.SendChatRoomUpdateToUser(member.UserId!, roomDto);
            }
        }

        return true;
    }

    private async Task<ChatRoomDto?> GetUpdatedRoomDto(int roomId, CancellationToken cancellationToken)
    {
        var room = await _context.ChatRooms
            .AsNoTracking() 
            .Include(r => r.Members)
            .ThenInclude(m => m.User) 
            .Include(r => r.Messages)
            .FirstOrDefaultAsync(r => r.Id == roomId, cancellationToken);

       
        if (room == null)
        {
            return null;
        }

        var roomDto = _mapper.Map<ChatRoomDto>(room);

        var lastMessage = room.Messages.LastOrDefault();
        if (lastMessage != null)
        {
            // اگر بخواهیم نام فرستنده را هم داشته باشیم، باید Sender را هم Include کنیم
            // roomDto.LastMessageSenderName = lastMessage.Sender.FullName;
        }

        //todo UnreadCount را باید محاسبه کنید

        return roomDto;
    }

}
