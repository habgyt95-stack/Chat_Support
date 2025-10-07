namespace Chat_Support.Application.Common.Interfaces;

public interface IPresenceTracker
{
    void RegisterConnection(int userId, string connectionId);
    void UnregisterConnection(string connectionId);
    void SetActiveRoom(string connectionId, int roomId);
    void ClearActiveRoom(string connectionId);
    bool IsUserViewingRoom(int userId, int roomId);
}
