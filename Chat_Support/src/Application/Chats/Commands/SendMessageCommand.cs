using Chat_Support.Application.Chats.DTOs;
using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Domain.Entities;
using Chat_Support.Domain.Enums;


namespace Chat_Support.Application.Chats.Commands;

public record SendMessageCommand(
    int ChatRoomId,
    string Content,
    MessageType Type = MessageType.Text,
    string? AttachmentUrl = null,
    int? ReplyToMessageId = null,
    string? GuestSessionId = null 
) : IRequest<ChatMessageDto>;

public class SendMessageCommandHandler : IRequestHandler<SendMessageCommand, ChatMessageDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IChatHubService _chatHubService;
    private readonly IUser _user;
    private readonly IMapper _mapper; 
    private readonly INewMessageNotifier _notifier;

    public SendMessageCommandHandler(
        IApplicationDbContext context,
        IChatHubService chatHubService,
        IUser user,
        IMapper mapper, 
        INewMessageNotifier notifier)
    {
        _context = context;
        _chatHubService = chatHubService;
        _user = user;
        _mapper = mapper; 
        _notifier = notifier;
    }

    public async Task<ChatMessageDto> Handle(SendMessageCommand request, CancellationToken cancellationToken)
    {
        var senderUserId = _user.Id;

        var chatRoom = await _context.ChatRooms
            .AsNoTracking()
            .Include(cr => cr.Members).ThenInclude(m => m.User) 
            .FirstOrDefaultAsync(cr => cr.Id == request.ChatRoomId, cancellationToken)
            ?? throw new KeyNotFoundException($"Chat room with Id {request.ChatRoomId} not found.");

        bool isGuest = senderUserId == 0 || senderUserId == -1;
        GuestUser? guestUser = null;
        if (isGuest)
        {
            // Validate guest access to room via session id
            if (string.IsNullOrEmpty(request.GuestSessionId) || chatRoom.GuestIdentifier != request.GuestSessionId)
                throw new UnauthorizedAccessException("Guest is not allowed in this chat room.");

            // Load guest user for display purposes
            guestUser = await _context.GuestUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.SessionId == request.GuestSessionId, cancellationToken);
        }
        else
        {
            if (!chatRoom.Members.Any(m => m.UserId == senderUserId))
                throw new UnauthorizedAccessException("User is not a member of this chat room.");
        }

        var message = new ChatMessage
        {
            Content = request.Content,
            SenderId = isGuest ? null : senderUserId,
            ChatRoomId = request.ChatRoomId,
            Type = request.Type,
            AttachmentUrl = request.AttachmentUrl,
            ReplyToMessageId = request.ReplyToMessageId
        };

        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);

        
        var messageToMap = await _context.ChatMessages
            .AsNoTracking()
            .Include(m => m.Sender)
            .Include(m => m.ReplyToMessage).ThenInclude(rpm => rpm!.Sender) 
            .FirstAsync(m => m.Id == message.Id, cancellationToken);

        var messageDto = _mapper.Map<ChatMessageDto>(messageToMap);

        // For guest messages, enrich DTO with guest display name if needed
        if (isGuest)
        {
            if (string.IsNullOrWhiteSpace(messageDto.SenderFullName))
            {
                messageDto.SenderFullName = guestUser?.Name ?? "مهمان";
            }
        }

        await _chatHubService.SendMessageToRoom(request.ChatRoomId.ToString(), messageDto);

        
        if (!isGuest)
        {
            var senderUser = chatRoom.Members.First(m => m.UserId == senderUserId).User;
            foreach (var member in chatRoom.Members)
            {
                
                if (member.UserId == senderUserId) continue;

               
                var roomUpdateDto = _mapper.Map<ChatRoomDto>(chatRoom);

                
                roomUpdateDto.UnreadCount = await _context.ChatMessages
                    .CountAsync(m => m.ChatRoomId == request.ChatRoomId &&
                                     m.SenderId != member.UserId &&
                                     m.Id > (member.LastReadMessageId ?? 0), cancellationToken);

                
                roomUpdateDto.LastMessageContent = message.Content;
                roomUpdateDto.LastMessageTime = message.Created.DateTime;
                roomUpdateDto.LastMessageSenderName = $"{senderUser.FirstName} {senderUser.LastName}";

                
                if (!chatRoom.IsGroup)
                {
                    roomUpdateDto.Name = $"{senderUser.FirstName} {senderUser.LastName}";
                    roomUpdateDto.Avatar = senderUser.ImageName;
                }

                await _chatHubService.SendChatRoomUpdateToUser(member.UserId, roomUpdateDto);
            }
        }
        else
        {
            // Sender is a guest: update chat room list for all authenticated members (agents)
            var guestDisplayName = guestUser?.Name ?? "مهمان";

            foreach (var member in chatRoom.Members)
            {
                // member.UserId is the authenticated user id (agent). Send them updates.
                var roomUpdateDto = _mapper.Map<ChatRoomDto>(chatRoom);

                roomUpdateDto.UnreadCount = await _context.ChatMessages
                    .CountAsync(m => m.ChatRoomId == request.ChatRoomId &&
                                     m.SenderId != member.UserId &&
                                     m.Id > (member.LastReadMessageId ?? 0), cancellationToken);

                roomUpdateDto.LastMessageContent = message.Content;
                roomUpdateDto.LastMessageTime = message.Created.DateTime;
                roomUpdateDto.LastMessageSenderName = guestDisplayName;

                if (!chatRoom.IsGroup)
                {
                    roomUpdateDto.Name = guestDisplayName;
                    roomUpdateDto.Avatar = null; // guests have no avatar
                }

                await _chatHubService.SendChatRoomUpdateToUser(member.UserId, roomUpdateDto);
            }
        }

        // Fire push notifications to members not currently viewing this room
        await _notifier.NotifyAsync(message, chatRoom, isGuest ? guestUser : null, cancellationToken);

        return messageDto;
    }
}
