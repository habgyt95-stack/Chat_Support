namespace Chat_Support.Application.Common.Interfaces;

public interface IMessageNotificationService
{
    Task SendNewMessageNotificationAsync(
        int recipientUserId,
        int chatRoomId,
        string title,
        string body,
        IDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default);
}
