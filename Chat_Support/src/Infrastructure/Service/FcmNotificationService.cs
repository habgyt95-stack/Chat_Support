using System.Net.Http.Headers;
using System.Text.Json;
using Chat_Support.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Chat_Support.Infrastructure.Service;

public record FcmMessageDto(string? token, string? topic, string title, string body, Dictionary<string, string>? dataDictionary);

public interface IFcmNotificationService : IMessageNotificationService { }

/// <summary>
/// Sends FCM notifications directly using HTTP v1 API without external proxy.
/// Avoids FirebaseAdmin package to respect central package management in this repo.
/// </summary>
public class FcmNotificationService : IFcmNotificationService
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<FcmNotificationService> _logger;
    private readonly IFirebaseAccessTokenProvider _tokenProvider;
    private readonly HttpClient _httpClient;

    public FcmNotificationService(
        IApplicationDbContext db,
        ILogger<FcmNotificationService> logger,
        IFirebaseAccessTokenProvider tokenProvider,
        IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _logger = logger;
        _tokenProvider = tokenProvider;
        _httpClient = httpClientFactory.CreateClient(nameof(FcmNotificationService));
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task SendNewMessageNotificationAsync(
        int recipientUserId,
        int chatRoomId,
        string title,
        string body,
        IDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Load recipient mobile (to fan-out notifications across accounts sharing the same phone)
            var recipientMobile = await _db.KciUsers
                .Where(u => u.Id == recipientUserId)
                .Select(u => u.Mobile)
                .FirstOrDefaultAsync(cancellationToken);

            // Query tokens by:
            // 1) direct binding to user
            // 2) deviceIds known for this user
            // 3) any account that shares the same mobile number as the recipient (if available)
            var userFcmTokensQuery = _db.UserFcmTokenInfoMobileAbrikChats
                .Where(f => f.FcmToken != null && f.FcmToken != "")
                .Where(f =>
                    f.UserId == recipientUserId
                    || (f.DeviceId != null && _db.AbrikChatUsersTokens.Any(t => t.UserId == recipientUserId && !t.IsRevoked && t.DeviceId != null && t.DeviceId == f.DeviceId))
                    || (!string.IsNullOrEmpty(recipientMobile) && f.User != null && f.User.Mobile == recipientMobile)
                )
                .Select(f => f.FcmToken!);

            var userFcmTokens = await userFcmTokensQuery
                .Distinct()
                .ToListAsync(cancellationToken);

            if (userFcmTokens.Count == 0)
            {
                _logger.LogInformation("No FCM tokens for user {UserId}. Skipping notification.", recipientUserId);
                return;
            }

            var payloadData = new Dictionary<string, string>
            {
                ["chatRoomId"] = chatRoomId.ToString()
            };
            if (data != null)
            {
                foreach (var kv in data) payloadData[kv.Key] = kv.Value;
            }

            var projectId = _tokenProvider.GetProjectId();
            var endpoint = $"https://fcm.googleapis.com/v1/projects/{projectId}/messages:send";
            var accessToken = await _tokenProvider.GetAccessTokenAsync(cancellationToken);

            foreach (var token in userFcmTokens)
            {
                var requestBody = new FcmHttpV1Request
                {
                    message = new FcmHttpV1Message
                    {
                        token = token,
                        notification = new FcmHttpV1Notification { title = title, body = body },
                        data = payloadData
                    }
                };

                using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = new StringContent(JsonSerializer.Serialize(requestBody))
                };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                using var resp = await _httpClient.SendAsync(req, cancellationToken);
                if (!resp.IsSuccessStatusCode)
                {
                    var respText = await resp.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("FCM send failed (user={UserId}, status={Status}): {Body}", recipientUserId, (int)resp.StatusCode, respText);
                }
                else
                {
                    _logger.LogDebug("FCM sent to user {UserId} token {TokenPrefix}...", recipientUserId, token[..Math.Min(8, token.Length)]);
                }
            }
        }
        catch (Exception ex)
        {
            // Do not crash chat flow if notifications fail
            _logger.LogError(ex, "Error sending FCM notification for user {UserId}", recipientUserId);
        }
    }

    private sealed class FcmHttpV1Request
    {
        public FcmHttpV1Message? message { get; set; }
    }

    private sealed class FcmHttpV1Message
    {
        public string? token { get; set; }
        public string? topic { get; set; }
        public FcmHttpV1Notification? notification { get; set; }
        public Dictionary<string, string>? data { get; set; }
    }

    private sealed class FcmHttpV1Notification
    {
        public string? title { get; set; }
        public string? body { get; set; }
    }
}
