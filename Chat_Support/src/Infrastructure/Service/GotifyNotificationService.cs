using System.Net.Http.Json;
using System.Text.Json;
using Chat_Support.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Chat_Support.Infrastructure.Service;

/// <summary>
/// سرویس ارسال نوتیفیکیشن با استفاده از Gotify (self-hosted push notification)
/// Gotify یک سرویس open-source برای ارسال push notification است
/// </summary>
public class GotifyNotificationService : IMessageNotificationService
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<GotifyNotificationService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _gotifyUrl;
    private readonly bool _isEnabled;

    public GotifyNotificationService(
        IApplicationDbContext db,
        ILogger<GotifyNotificationService> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _db = db;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(nameof(GotifyNotificationService));
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
        
        _gotifyUrl = configuration["Gotify:ServerUrl"] ?? "http://localhost:8080";
        _isEnabled = configuration.GetValue<bool>("Gotify:Enabled", true);
    }

    public async Task SendNewMessageNotificationAsync(
        int recipientUserId,
        int chatRoomId,
        string title,
        string body,
        IDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        if (!_isEnabled)
        {
            _logger.LogDebug("Gotify notification disabled. Skipping notification for user {UserId}", recipientUserId);
            return;
        }

        try
        {
            // دریافت توکن‌های Gotify کاربر از جدول موجود
            // از همان جدول FcmToken استفاده می‌کنیم ولی برای Gotify
            var gotifyTokens = await _db.UserFcmTokenInfoMobileAbrikChats
                .Where(t => t.UserId == recipientUserId && !string.IsNullOrEmpty(t.FcmToken))
                .Select(t => t.FcmToken!)
                .ToListAsync(cancellationToken);

            if (gotifyTokens.Count == 0)
            {
                _logger.LogDebug("User {UserId} has no Gotify tokens. Skipping notification.", recipientUserId);
                return;
            }

            // ساخت payload برای Gotify
            var extras = new Dictionary<string, object>();
            if (data != null)
            {
                foreach (var kvp in data)
                {
                    extras[kvp.Key] = kvp.Value;
                }
            }
            extras["chatRoomId"] = chatRoomId;
            extras["recipientUserId"] = recipientUserId;

            var payload = new
            {
                title = title,
                message = body,
                priority = 5, // اولویت متوسط (0-10)
                extras = extras
            };

            // ارسال به تمام توکن‌های Gotify کاربر (چند دستگاه)
            int successCount = 0;
            foreach (var gotifyToken in gotifyTokens)
            {
                try
                {
                    var response = await _httpClient.PostAsJsonAsync(
                        $"{_gotifyUrl}/message?token={gotifyToken}",
                        payload,
                        cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        successCount++;
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                        _logger.LogWarning("Failed to send notification to user {UserId} token. Status: {StatusCode}, Error: {Error}",
                            recipientUserId, response.StatusCode, errorContent);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error sending notification to one device for user {UserId}", recipientUserId);
                }
            }

            if (successCount > 0)
            {
                _logger.LogInformation("Notification sent successfully to {Count} device(s) for user {UserId}, chatRoom {ChatRoomId}",
                    successCount, recipientUserId, chatRoomId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notification to user {UserId} for chatRoom {ChatRoomId}",
                recipientUserId, chatRoomId);
        }
    }
}
