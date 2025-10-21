using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Chat_Support.Application.Auth.Commands;
using Chat_Support.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Dynamic;

namespace Chat_Support.Web.Endpoints;

public class AuthEndpoints : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/auth");
        group.MapGet("/verify-token", VerifyToken);
        group.MapGet("/profile", GetProfile).RequireAuthorization();
        group.MapPost("/request-otp", RequestOtp).AllowAnonymous(); 
        group.MapPost("/verify-otp", VerifyOtp).AllowAnonymous(); 
        group.MapPost("/login", AbrikChatLogin).AllowAnonymous();
        group.MapPost("/refresh-token", RefreshToken);
        group.MapPost("/authenticate-from-app", AuthenticateFromApp)
            .AllowAnonymous()
            .WithName("AuthenticateFromApp")
            .Produces<AuthResultDto>()
            .Produces(StatusCodes.Status401Unauthorized)
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = "JwtAppChat" });

        // Legacy external routes kept intact
        app.MapPost("/api/AbrikChatAccount/AbrikChatLogin", AbrikChatLogin).AllowAnonymous();
        app.MapPost("/api/AbrikChatAccount/refreshToken", AbrikChatRefreshToken).AllowAnonymous();
    }

    private static bool IsWebClient(HttpContext httpContext)
    {
        var ua = httpContext.Request.Headers.UserAgent.ToString();
        if (string.IsNullOrEmpty(ua)) return false;
        // Basic heuristic for browser UAs
        return ua.Contains("Mozilla", StringComparison.OrdinalIgnoreCase)
            || ua.Contains("Chrome", StringComparison.OrdinalIgnoreCase)
            || ua.Contains("Safari", StringComparison.OrdinalIgnoreCase)
            || ua.Contains("Edg", StringComparison.OrdinalIgnoreCase)
            || ua.Contains("Firefox", StringComparison.OrdinalIgnoreCase);
    }

    private static object BuildLoginResult(int userId, string username, Chat_Support.Application.Auth.DTOs.AuthTokens tokens)
    {
        dynamic loginResult = new ExpandoObject();
        loginResult.AccessToken = tokens.AccessToken;
        loginResult.RefreshToken = tokens.RefreshToken;
        loginResult.userId = userId;
        loginResult.username = username;
        loginResult.homepage = "/chat";
        loginResult.domain = "https://chat.abrik.cloud";
        return loginResult;
    }

    public IResult VerifyToken([FromQuery] string token, HttpContext httpContext)
    {
        var user = httpContext.User;
        if (user.Identity is { IsAuthenticated: false })
        {
            return Results.Unauthorized();
        }

        // Prefer NameIdentifier (numeric user id) to align with chat SenderId; fallback to 'sub'
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub")!;

        var profile = new UserProfileDto
        {
            Id = id,
            Username = user.FindFirstValue(ClaimTypes.Name)!,
            FirstName = user.FindFirstValue("firstname")!,
            LastName = user.FindFirstValue("lastname")!,
            RegionId = user.FindFirstValue("regionId")!
        };

        return Results.Ok(new { Token = token, Profile = profile });
    }

    public IResult GetProfile(ClaimsPrincipal user)
    {
        // Prefer NameIdentifier (numeric user id) to align with chat SenderId; fallback to 'sub'
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub")!;

        var profile = new UserProfileDto
        {
            Id = id,
            Username = user.FindFirstValue(ClaimTypes.Name)!,
            FirstName = user.FindFirstValue("firstname")!,
            LastName = user.FindFirstValue("lastname")!,
            RegionId = user.FindFirstValue("regionId")!
        };

        return Results.Ok(profile);
    }

    private async Task<IResult> RequestOtp([FromBody] RequestOtpCommand request, ISender mediator, HttpContext httpContext)
    {
        var result = await mediator.Send(request);
        if (result is null || !result.Sent || result.VerifyNumber is null)
        {
            return Results.BadRequest("شماره موبایل نامعتبر است یا درخواست شما بیش از حد مجاز بوده است.");
        }

        // Set explicit headers as requested (CORS headers are typically handled by middleware)
        httpContext.Response.Headers["access-control-allow-credentials"] = "true";
        httpContext.Response.Headers["access-control-allow-origin"] = "https://chat.abrik.cloud";
        httpContext.Response.Headers["vary"] = "Origin";

        return Results.Json(new { twoStepVertification = true, verifyNumber = result.VerifyNumber.Value },
            statusCode: StatusCodes.Status200OK,
            contentType: "application/json; charset=utf-8");
    }

    private async Task<IResult> VerifyOtp([FromBody] VerifyOtpCommand request, ISender mediator)
    {
        var result = await mediator.Send(request);
        return Results.Ok(result);
    }

    private async Task<IResult> AbrikChatLogin(
        [FromBody] AbrikChatLoginRequest request,
        IApplicationDbContext db,
        ISender mediator,
        HttpContext httpContext)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.UserName))
            return Results.BadRequest("درخواست نامعتبر است");

        var user = await db.KciUsers.FirstOrDefaultAsync(u => u.UserName == request.UserName && u.Enable == true);
        if (user == null)
            return Results.BadRequest("نام کاربری/شماره موبایل یافت نشد");

        var providedDeviceId = request.DeviceInfo?.DeviceId?.Trim();

        // بررسی تطابق دیوایس با رکورد موجود (برای همه به جز وب و کاربر توسعه‌دهنده با شناسه 3094)
        var existingDeviceRecord = await db.AbrikChatUsersTokens
            .Where(t => t.UserId == user.Id && !t.IsRevoked)
            .OrderByDescending(t => t.IssuedAt)
            .FirstOrDefaultAsync();

        var isWeb = IsWebClient(httpContext);
        if (!isWeb && user.Id != 3094 && existingDeviceRecord != null && !string.IsNullOrEmpty(existingDeviceRecord.DeviceId))
        {
            if (!string.Equals(existingDeviceRecord.DeviceId, providedDeviceId, StringComparison.Ordinal))
            {
                return Results.BadRequest("شما مجاز به استفاده از این دستگاه نمی باشید");
            }
        }

        // اگر verifyCode خالی یا 0 بود، ارسال OTP
        if (!request.VerifyCode.HasValue || request.VerifyCode.Value == 0)
        {
            var otpResult = await mediator.Send(new RequestOtpCommand(request.UserName));
            if (otpResult is null || !otpResult.Sent || otpResult.VerifyNumber is null)
                return Results.BadRequest("شماره موبایل نامعتبر است یا درخواست شما بیش از حد مجاز بوده است.");

            // Set explicit headers as requested
            httpContext.Response.Headers["access-control-allow-credentials"] = "true";
            httpContext.Response.Headers["access-control-allow-origin"] = "https://chat.abrik.cloud";
            httpContext.Response.Headers["vary"] = "Origin";

            return Results.Json(new { twoStepVertification = true, verifyNumber = otpResult.VerifyNumber.Value },
                statusCode: StatusCodes.Status200OK,
                contentType: "application/json; charset=utf-8");
        }

        // تایید کد
        var tokens = await mediator.Send(new VerifyOtpCommand(request.UserName, request.VerifyCode.Value.ToString()));
        if (tokens == null)
            return Results.BadRequest("کد وارد شده صحیح نیست یا منقضی شده است.");

        // ثبت/به‌روزرسانی رفرش‌توکن برای وب
        var refreshTokenEntity = new ChatUserRefreshToken
        {
            UserId = user.Id,
            CreationDate = DateTime.Now,
            Token = tokens.RefreshToken,
            ExpirationTime = DateTime.Now.AddDays(30),
            IsRevoked = false,
        };
        await db.ChatUserRefreshTokens.AddAsync(refreshTokenEntity);

        // ثبت یا به‌روزرسانی رکورد دستگاه کاربر
        if (existingDeviceRecord == null)
        {
            var deviceRecord = new AbrikChatUsersToken
            {
                UserId = user.Id,
                DeviceId = providedDeviceId,
                RefreshToken = tokens.RefreshToken,
                IssuedAt = DateTime.Now,
                ExpiresAt = DateTime.Now.AddDays(30),
                IsRevoked = false
            };
            await db.AbrikChatUsersTokens.AddAsync(deviceRecord);
        }
        else
        {
            existingDeviceRecord.RefreshToken = tokens.RefreshToken;
            existingDeviceRecord.IssuedAt = DateTime.Now;
            existingDeviceRecord.ExpiresAt = DateTime.Now.AddDays(30);
            existingDeviceRecord.IsRevoked = false;
        }

        // پیوند دادن FCM token های ثبت‌شده این دستگاه به کاربر تازه لاگین شده
        if (!string.IsNullOrWhiteSpace(providedDeviceId))
        {
            var fcmRows = await db.UserFcmTokenInfoMobileAbrikChats
                .Where(x => x.DeviceId == providedDeviceId)
                .ToListAsync();
            foreach (var r in fcmRows)
            {
                r.UserId = user.Id;
            }
        }

        await db.SaveChangesAsync(CancellationToken.None);

        // Explicit headers for successful login response
        httpContext.Response.Headers["access-control-allow-credentials"] = "true";
        httpContext.Response.Headers["access-control-allow-origin"] = "https://chat.abrik.cloud";
        httpContext.Response.Headers["vary"] = "Origin";

        return Results.Json(BuildLoginResult(user.Id, user.UserName, tokens),
            statusCode: StatusCodes.Status200OK,
            contentType: "application/json; charset=utf-8");
    }

    private async Task<IResult> AbrikChatRefreshToken(
        [FromBody] AbrikChatRefreshTokenRequest request,
        IApplicationDbContext db,
        IJwtService jwtService,
        HttpContext httpContext)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.AccessToken) || string.IsNullOrWhiteSpace(request.RefreshToken))
            return Results.BadRequest("درخواست نامعتبر است");

        ClaimsPrincipal principal;
        try
        {
            principal = jwtService.GetPrincipalFromExpiredToken(request.AccessToken);
        }
        catch
        {
            return Results.Unauthorized();
        }

        var userIdString = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        if (!int.TryParse(userIdString, out var userId))
            return Results.Unauthorized();

        var deviceId = request.DeviceId?.Trim();

        // Device binding enforcement (skip for web and developer user 3094)
        var existingDeviceRecord = await db.AbrikChatUsersTokens
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .OrderByDescending(t => t.IssuedAt)
            .FirstOrDefaultAsync();

        var isWeb = IsWebClient(httpContext);
        if (!isWeb && userId != 3094 && existingDeviceRecord != null && !string.IsNullOrEmpty(existingDeviceRecord.DeviceId))
        {
            if (!string.Equals(existingDeviceRecord.DeviceId, deviceId, StringComparison.Ordinal))
            {
                return Results.BadRequest("شما مجاز به استفاده از این دستگاه نمی باشید");
            }
        }

        // Validate existing refresh token
        var savedRefreshToken = await db.ChatUserRefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken && rt.UserId == userId && !rt.IsRevoked && rt.ExpirationTime > DateTime.Now);

        if (savedRefreshToken == null)
            return Results.Unauthorized();

        var user = await db.KciUsers.FindAsync(userId);
        if (user == null)
            return Results.Unauthorized();

        // Revoke old and issue new tokens
        savedRefreshToken.IsRevoked = true;

        var tokens = await jwtService.GenerateTokensAsync(user);
        if (tokens == null)
            return Results.Unauthorized();

        var newRefreshTokenEntity = new ChatUserRefreshToken
        {
            UserId = userId,
            CreationDate = DateTime.Now,
            Token = tokens.RefreshToken,
            ExpirationTime = DateTime.Now.AddDays(30),
            IsRevoked = false,
        };
        await db.ChatUserRefreshTokens.AddAsync(newRefreshTokenEntity);

        // Update device token record
        if (existingDeviceRecord == null)
        {
            await db.AbrikChatUsersTokens.AddAsync(new AbrikChatUsersToken
            {
                UserId = userId,
                DeviceId = deviceId,
                RefreshToken = tokens.RefreshToken,
                IssuedAt = DateTime.Now,
                ExpiresAt = DateTime.Now.AddDays(30),
                IsRevoked = false
            });
        }
        else
        {
            existingDeviceRecord.RefreshToken = tokens.RefreshToken;
            existingDeviceRecord.IssuedAt = DateTime.Now;
            existingDeviceRecord.ExpiresAt = DateTime.Now.AddDays(30);
            existingDeviceRecord.IsRevoked = false;
        }

        // پیوند FCM این دستگاه به این کاربر در رفرش توکن نیز
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            var fcmRows = await db.UserFcmTokenInfoMobileAbrikChats
                .Where(x => x.DeviceId == deviceId)
                .ToListAsync();
            foreach (var r in fcmRows)
            {
                r.UserId = userId;
            }
        }

        await db.SaveChangesAsync(CancellationToken.None);

        return Results.Ok(BuildLoginResult(user.Id, user.UserName, tokens));
    }

    private async Task<IResult> RefreshToken([FromBody] RefreshTokenCommand request, ISender mediator)
    {
        var result = await mediator.Send(request);
        if (result == null)
            return Results.Unauthorized();
        return Results.Ok(result);
    }

    private async Task<IResult> AuthenticateFromApp([FromBody] AuthenticateFromAppCommand request, ISender mediator)
    {
        var result = await mediator.Send(request);
        if (result == null)
            return Results.Unauthorized();
        return Results.Ok(result);
    }
}

// یک DTO برای اطلاعات پروفایل کاربر تعریف کنید.
// می‌توانید این کلاس را در یک فایل جداگانه در پروژه Application قرار دهید.
// مثلا: Application/Users/DTOs/UserProfileDto.cs
public class UserProfileDto
{
    public string? Id { get; set; }
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? RegionId { get; set; }
}

public sealed class AbrikChatLoginRequest
{
    public string UserName { get; set; } = string.Empty;
    public DeviceInfoDto? DeviceInfo { get; set; }
    public int? VerifyCode { get; set; }
}

public sealed class AbrikChatRefreshTokenRequest
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string? DeviceId { get; set; }
}

public sealed class DeviceInfoDto
{
    public string? DeviceId { get; set; }
    public string? Ip { get; set; }
}
