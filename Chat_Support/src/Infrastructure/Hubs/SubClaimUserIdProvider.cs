using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace Chat_Support.Infrastructure.Hubs;

// Ensures Clients.User("{userId}") targets connections where JWT contains that user id in the "sub" (subject) claim.
public class SubClaimUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        var user = connection?.User;
        if (user == null) return null;

        // Prefer standard JWT subject claim (sub)
        var sub = user.FindFirst("sub")?.Value;
        if (!string.IsNullOrEmpty(sub)) return sub;

        // Fallbacks if token mapping differs
        var nameId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? user.FindFirst("nameid")?.Value
                      ?? user.FindFirst("userId")?.Value
                      ?? user.FindFirst("userid")?.Value;
        return nameId;
    }
}
