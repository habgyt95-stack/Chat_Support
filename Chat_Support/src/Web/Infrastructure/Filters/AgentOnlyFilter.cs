using Chat_Support.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Chat_Support.Web.Infrastructure.Filters;

/// <summary>
/// Endpoint filter که بررسی می‌کند آیا کاربر فعلی در جدول Support_Agents وجود دارد یا خیر
/// اگر کاربر Support Agent نباشد، خطای 403 Forbidden برمی‌گرداند
/// </summary>
public class AgentOnlyFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        
        // بررسی احراز هویت
        if (httpContext.User?.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }

        // دریافت UserId از token
        var userIdClaim = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Results.Problem(
                title: "Invalid User ID",
                detail: "توکن احراز هویت معتبر نیست",
                statusCode: StatusCodes.Status401Unauthorized
            );
        }

        // بررسی وجود کاربر در جدول Support_Agents
        var dbContext = httpContext.RequestServices.GetRequiredService<IApplicationDbContext>();
        
        var isAgent = await dbContext.SupportAgents
            .AnyAsync(a => a.UserId == userId && a.IsActive);

        if (!isAgent)
        {
            return Results.Problem(
                title: "Access Denied",
                detail: "شما به عنوان پشتیبان ثبت نشده‌اید یا حساب شما غیرفعال است",
                statusCode: StatusCodes.Status403Forbidden
            );
        }

        // کاربر یک Agent معتبر است، ادامه پردازش
        return await next(context);
    }
}
