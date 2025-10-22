using Chat_Support.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Chat_Support.Infrastructure.Service;

/// <summary>
/// پیاده‌سازی IPresenceTracker با استفاده از Redis
/// برای پشتیبانی از SignalR در حالت scale-out (چند instance)
/// </summary>
public class RedisPresenceTracker : IPresenceTracker
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisPresenceTracker> _logger;
    private readonly IDatabase _db;

    public RedisPresenceTracker(
        IConnectionMultiplexer redis,
        ILogger<RedisPresenceTracker> logger)
    {
        _redis = redis;
        _logger = logger;
        _db = redis.GetDatabase();
    }

    public void RegisterConnection(int userId, string connectionId)
    {
        try
        {
            // اضافه کردن connectionId به set مربوط به user
            var key = $"presence:user:{userId}:connections";
            _db.SetAdd(key, connectionId);
            _db.KeyExpire(key, TimeSpan.FromHours(24));

            // ذخیره اطلاعات connection
            var infoKey = $"presence:connection:{connectionId}";
            _db.HashSet(infoKey, new HashEntry[]
            {
                new("UserId", userId),
                new("ConnectedAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            });
            _db.KeyExpire(infoKey, TimeSpan.FromHours(24));

            _logger.LogDebug("Registered connection {ConnectionId} for user {UserId} in Redis", 
                connectionId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering connection {ConnectionId} for user {UserId} in Redis", 
                connectionId, userId);
        }
    }

    public void UnregisterConnection(string connectionId)
    {
        try
        {
            // دریافت userId
            var infoKey = $"presence:connection:{connectionId}";
            var userIdValue = _db.HashGet(infoKey, "UserId");

            if (userIdValue.HasValue)
            {
                var userId = (int)userIdValue;
                var key = $"presence:user:{userId}:connections";
                _db.SetRemove(key, connectionId);
                _logger.LogDebug("Unregistered connection {ConnectionId} for user {UserId} from Redis", 
                    connectionId, userId);
            }

            // حذف اطلاعات connection
            _db.KeyDelete(infoKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unregistering connection {ConnectionId} from Redis", connectionId);
        }
    }

    public void SetActiveRoom(string connectionId, int roomId)
    {
        try
        {
            var infoKey = $"presence:connection:{connectionId}";
            _db.HashSet(infoKey, "ActiveRoomId", roomId);
            _logger.LogDebug("Set active room {RoomId} for connection {ConnectionId} in Redis", 
                roomId, connectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting active room for connection {ConnectionId} in Redis", connectionId);
        }
    }

    public void ClearActiveRoom(string connectionId)
    {
        try
        {
            var infoKey = $"presence:connection:{connectionId}";
            _db.HashDelete(infoKey, "ActiveRoomId");
            _logger.LogDebug("Cleared active room for connection {ConnectionId} in Redis", connectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing active room for connection {ConnectionId} in Redis", connectionId);
        }
    }

    public bool IsUserViewingRoom(int userId, int roomId)
    {
        try
        {
            var key = $"presence:user:{userId}:connections";
            var connections = _db.SetMembers(key);

            foreach (var connection in connections)
            {
                var infoKey = $"presence:connection:{connection}";
                var activeRoomValue = _db.HashGet(infoKey, "ActiveRoomId");

                if (activeRoomValue.HasValue && (int)activeRoomValue == roomId)
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user {UserId} is viewing room {RoomId} in Redis", 
                userId, roomId);
            return false;
        }
    }

    /// <summary>
    /// تعداد کاربران آنلاین
    /// </summary>
    public int GetOnlineUsersCount()
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var keys = server.Keys(pattern: "presence:user:*:connections");
            return keys.Count();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting online users count from Redis");
            return 0;
        }
    }
}
