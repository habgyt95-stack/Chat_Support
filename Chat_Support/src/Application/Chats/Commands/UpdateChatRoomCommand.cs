using Chat_Support.Application.Chats.DTOs;
using Chat_Support.Application.Common.Exceptions;
using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Domain.Entities;

namespace Chat_Support.Application.Chats.Commands;

public record UpdateChatRoomCommand(
    int ChatRoomId,
    string? Name = null,
    string? Description = null
) : IRequest<ChatRoomDto>;

public class UpdateChatRoomCommandHandler : IRequestHandler<UpdateChatRoomCommand, ChatRoomDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IChatHubService _chatHubService;
    private readonly IMapper _mapper;

    public UpdateChatRoomCommandHandler(
        IApplicationDbContext context,
        IUser user,
        IChatHubService chatHubService,
        IMapper mapper)
    {
        _context = context;
        _user = user;
        _chatHubService = chatHubService;
        _mapper = mapper;
    }

    public async Task<ChatRoomDto> Handle(UpdateChatRoomCommand request, CancellationToken cancellationToken)
    {
        var userId = _user.Id;

        var chatRoom = await _context.ChatRooms
            .Include(r => r.Members)
            .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(r => r.Id == request.ChatRoomId, cancellationToken);

        if (chatRoom == null)
            throw new KeyNotFoundException($"Chat room with Id {request.ChatRoomId} not found.");

        // Check if user is owner or admin
        var member = chatRoom.Members.FirstOrDefault(m => m.UserId == userId);
        if (member == null || (member.Role != Domain.Enums.ChatRole.Owner && member.Role != Domain.Enums.ChatRole.Admin))
            throw new UnauthorizedAccessException("Only group owner or admin can update group information.");

        // Update name if provided
        if (!string.IsNullOrWhiteSpace(request.Name))
            chatRoom.Name = request.Name;

        // Update description if provided
        if (request.Description != null)
            chatRoom.Description = request.Description;

        await _context.SaveChangesAsync(cancellationToken);

        // Notify all members
        var roomDto = _mapper.Map<ChatRoomDto>(chatRoom);
        foreach (var roomMember in chatRoom.Members)
        {
            await _chatHubService.SendChatRoomUpdateToUser(roomMember.UserId, roomDto);
        }

        return roomDto;
    }
}
