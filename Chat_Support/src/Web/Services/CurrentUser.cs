using System.Security.Claims;
using Chat_Support.Application.Common.Interfaces;

namespace Chat_Support.Web.Services;

public class CurrentUser : IUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CurrentUser> _logger;

    public CurrentUser(IHttpContextAccessor httpContextAccessor, ILogger<CurrentUser> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    // برای سازگاری با interface قدیمی
    public string? Id => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

    int IUser.Id
    {
        get
        {
            try
            {
                var context = _httpContextAccessor.HttpContext;
                if (context == null)
                {
                    _logger.LogWarning("HttpContext is null");
                    return -1;
                }

                var user = context.User;
                if (user.Identity is { IsAuthenticated: false })
                {
                    _logger.LogWarning("User is not authenticated");
                    return -1;
                }

                // لاگ تمام Claims برای دیباگ
                foreach (var claim in user.Claims)
                {
                    _logger.LogDebug($"Claim Type: {claim.Type}, Value: {claim.Value}");
                }

                // جستجوی claim با انواع مختلف
                var idClaim = user.FindFirstValue(ClaimTypes.NameIdentifier) ??
                             user.FindFirstValue("sub") ??
                             user.FindFirstValue("nameid") ??
                             user.FindFirstValue("id");

                if (string.IsNullOrEmpty(idClaim))
                {
                    _logger.LogWarning("No ID claim found in user claims");
                    return -1;
                }

                if (int.TryParse(idClaim, out var id))
                {
                    _logger.LogInformation($"User ID successfully parsed: {id}");
                    return id;
                }

                _logger.LogWarning($"Failed to parse user ID: {idClaim}");
                return -1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user ID");
                return -1;
            }
        }
    }

    int IUser.RegionId
    {
        get
        {
            try
            {
                var context = _httpContextAccessor.HttpContext;
                if (context?.User.Identity is { IsAuthenticated: false })
                {
                    return -1;
                }

                var regionIdClaim = context?.User.FindFirstValue("regionId") ??
                                   context?.User.FindFirstValue("region_id");

                if (int.TryParse(regionIdClaim, out var regionId))
                {
                    return regionId;
                }

                return -1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting region ID");
                return -1;
            }
        }
    }
}
