using System.Net.Http.Json;
using Chat_Support.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Chat_Support.Infrastructure.Service;

public record FcmMessageDto(string? token, string? topic, string title, string body, Dictionary<string, string>? dataDictionary);
public record FcmEnvelope(string refreshToken, FcmMessageDto fcmMessageDto);

public interface IFcmNotificationService : IMessageNotificationService { }

public class FcmNotificationService : IFcmNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly IApplicationDbContext _db;

    public FcmNotificationService(HttpClient httpClient, IApplicationDbContext db)
    {
        _httpClient = httpClient;
        _db = db;
        _httpClient.BaseAddress = new Uri("http://rdvs.abrik.cloud");
    }

    public async Task SendNewMessageNotificationAsync(
        int recipientUserId,
        int chatRoomId,
        string title,
        string body,
        IDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        // Read latest refresh token for user
        var refreshToken = await _db.AbrikChatUsersTokens
            .Where(t => t.UserId == recipientUserId && !t.IsRevoked)
            .OrderByDescending(t => t.IssuedAt)
            .Select(t => t.RefreshToken)
            .FirstOrDefaultAsync(cancellationToken);

        // Read active FCM tokens for user
        var userFcmTokens = await _db.UserFcmTokenInfoMobileAbrikChats
            .Where(f => f.UserId == recipientUserId && !string.IsNullOrEmpty(f.FcmToken))
            .Select(f => f.FcmToken!)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(refreshToken) || userFcmTokens.Count == 0)
            return; // nothing to notify

        var payloadData = new Dictionary<string, string>
        {
            ["chatRoomId"] = chatRoomId.ToString()
        };
        if (data != null)
        {
            foreach (var kv in data) payloadData[kv.Key] = kv.Value;
        }

        foreach (var fcm in userFcmTokens)
        {
            var envelope = new FcmEnvelope(
                refreshToken,
                new FcmMessageDto(
                    token: fcm,
                    topic: string.Empty,
                    title: title,
                    body: body,
                    dataDictionary: payloadData
                )
            );

            using var resp = await _httpClient.PostAsJsonAsync("/api/Fcm/sendAbrikChatFcm", envelope, cancellationToken);
            resp.EnsureSuccessStatusCode();
        }
    }
}
