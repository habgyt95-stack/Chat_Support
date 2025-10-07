using Chat_Support.Application.Common.Models;

namespace Chat_Support.Application.Common.Interfaces;
public interface IIdentityService
{
    Task<string?> GetUserNameAsync(int userId);

    Task<bool> IsInRoleAsync(int userId, string role);

    Task<bool> AuthorizeAsync(int userId, string policyName);

    Task<(Result Result, string UserId)> CreateUserAsync(string userName, string password);

    Task<Result> DeleteUserAsync(string userId);
}
