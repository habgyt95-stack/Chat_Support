using System.Security.Claims;
using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chat_Support.Web.Endpoints;

public class FcmEndpoints : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/Fcm");
        // Keep it open to match the provided cURL; we will still bind user if auth header exists
        group.MapPost("/addFcmInfoAbrikChat", AddFcmInfo).AllowAnonymous();
    }

    private static async Task<IResult> AddFcmInfo(
        [FromBody] FcmInfoRequest request,
        IApplicationDbContext db,
        ClaimsPrincipal claims,
        HttpContext httpContext)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.FcmToken) || string.IsNullOrWhiteSpace(request.DeviceId))
            return Results.BadRequest("??????? ??????? ???");

        int? userId = null;
        var idStr = claims.FindFirstValue(ClaimTypes.NameIdentifier) ?? claims.FindFirstValue("sub");
        if (int.TryParse(idStr, out var parsed))
        {
            userId = parsed;
        }

        var deviceId = request.DeviceId.Trim();
        var fcmToken = request.FcmToken.Trim();

        // Upsert by DeviceId; update FCM token and user binding if available
        var existing = await db.UserFcmTokenInfoMobileAbrikChats
            .FirstOrDefaultAsync(x => x.DeviceId == deviceId);

        var epochNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (existing == null)
        {
            var entity = new UserFcmTokenInfoMobileAbrikChat
            {
                UserId = userId,
                DeviceId = deviceId,
                FcmToken = fcmToken,
                AddedDate = epochNow
            };
            await db.UserFcmTokenInfoMobileAbrikChats.AddAsync(entity);
        }
        else
        {
            existing.FcmToken = fcmToken;
            existing.AddedDate = epochNow;
            if (userId.HasValue)
                existing.UserId = userId;
        }

        await db.SaveChangesAsync(CancellationToken.None);

        // Set explicit headers as requested
        httpContext.Response.Headers["access-control-allow-credentials"] = "true";
        httpContext.Response.Headers["access-control-allow-origin"] = "https://chat.abrik.cloud";
        httpContext.Response.Headers["vary"] = "Origin";

        return Results.Json(new { success = true },
            statusCode: StatusCodes.Status200OK,
            contentType: "application/json; charset=utf-8");
    }
}

public sealed class FcmInfoRequest
{
    public string FcmToken { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
}
