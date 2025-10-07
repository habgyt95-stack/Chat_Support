using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Domain.Entities;

namespace Chat_Support.Application.Chats.Commands;

// DTO for the reaction data sent from client and broadcasted
public record MessageReactionDto(int MessageId, int UserId, string UserFullName, string Emoji, int ChatRoomId, bool IsRemoved);
public record ReactToMessageCommand(int MessageId, string Emoji) : IRequest<MessageReactionDto>;
public class ReactToMessageCommandHandler : IRequestHandler<ReactToMessageCommand, MessageReactionDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IChatHubService _chatHubService;

    public ReactToMessageCommandHandler(IApplicationDbContext context, IUser user, IChatHubService chatHubService)
    {
        _context = context;
        _user = user;
        _chatHubService = chatHubService;
    }

    public async Task<MessageReactionDto> Handle(ReactToMessageCommand request, CancellationToken cancellationToken)
    {
        var userId = _user.Id;
        var userEntity = await _context.KciUsers.FindAsync(userId);
        if (userEntity == null)
        {
            throw new UnauthorizedAccessException("User not found.");
        }

        var message = await _context.ChatMessages.FindAsync(request.MessageId);
        if (message == null)
        {
            throw new KeyNotFoundException("Message not found.");
        }

        bool isRemoved = false;
        var existingReaction = await _context.MessageReactions
            .FirstOrDefaultAsync(r => r.MessageId == request.MessageId && r.UserId == userId && r.Emoji == request.Emoji, cancellationToken);

        if (existingReaction != null)
        {
            _context.MessageReactions.Remove(existingReaction);
            isRemoved = true;
        }
        else
        {
            var anyExistingReactionFromUser = await _context.MessageReactions
               .FirstOrDefaultAsync(r => r.MessageId == request.MessageId && r.UserId == userId, cancellationToken);
            if (anyExistingReactionFromUser != null)
            {
                _context.MessageReactions.Remove(anyExistingReactionFromUser);
            }

            var newReaction = new MessageReaction
            {
                MessageId = request.MessageId,
                UserId = userId,
                Emoji = request.Emoji
            };
            _context.MessageReactions.Add(newReaction);
            isRemoved = false;
        }

        await _context.SaveChangesAsync(cancellationToken);

        // UserFullName به جای UserName
        var userFullName = $" {userEntity.FirstName} {userEntity.LastName}";
        var reactionDto = new MessageReactionDto(request.MessageId, userId, userFullName, request.Emoji, message.ChatRoomId, isRemoved);

        await _chatHubService.SendMessageUpdateToRoom(message.ChatRoomId.ToString(), reactionDto, "MessageReacted");

        return reactionDto;
    }
}

