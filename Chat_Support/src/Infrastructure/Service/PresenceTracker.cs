using System.Collections.Concurrent;
using Chat_Support.Application.Common.Interfaces;

namespace Chat_Support.Infrastructure.Service;

public class PresenceTracker : IPresenceTracker
{
    private class ConnectionInfo
    {
        public int UserId { get; init; }
        public int? ActiveRoomId { get; set; }
    }

    private static readonly ConcurrentDictionary<string, ConnectionInfo> _connections = new();

    public void RegisterConnection(int userId, string connectionId)
        => _connections[connectionId] = new ConnectionInfo { UserId = userId };

    public void UnregisterConnection(string connectionId)
        => _connections.TryRemove(connectionId, out _);

    public void SetActiveRoom(string connectionId, int roomId)
    {
        if (_connections.TryGetValue(connectionId, out var info))
            info.ActiveRoomId = roomId;
    }

    public void ClearActiveRoom(string connectionId)
    {
        if (_connections.TryGetValue(connectionId, out var info))
            info.ActiveRoomId = null;
    }

    public bool IsUserViewingRoom(int userId, int roomId)
    {
        foreach (var kvp in _connections)
        {
            var info = kvp.Value;
            if (info.UserId == userId && info.ActiveRoomId == roomId)
                return true;
        }
        return false;
    }
}
