namespace Chat_Support.Application.Common.Interfaces;

using Chat_Support.Domain.Entities;

public interface INewMessageNotifier
{
    Task NotifyAsync(ChatMessage message, ChatRoom chatRoom, GuestUser? guestSender = null, CancellationToken cancellationToken = default);
}
