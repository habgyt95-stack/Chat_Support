using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Application.Common.Models;
using Chat_Support.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chat_Support.Infrastructure.Identity;
public class IdentityService : IIdentityService
{
    private readonly IApplicationDbContext _context;

    public IdentityService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string?> GetUserNameAsync(int userId)
    {
        var user = await _context.KciUsers.FirstOrDefaultAsync(u => u.Id == userId);
        return user?.UserName;
    }

    public async Task<bool> IsInRoleAsync(int userId, string role)
    {
        var user = await _context.KciUsers.FirstOrDefaultAsync(u => u.Id == userId);
        return user != null && user.Description == role;
    }

    public async Task<bool> AuthorizeAsync(int userId, string policyName)
    {
        return await Task.FromResult(false);
    }

    public async Task<(Result Result, string UserId)> CreateUserAsync(string userName, string password)
    {
        if (await _context.KciUsers.AnyAsync(u => u.UserName == userName))
            return (Result.Failure(new[] { "Username already exists" }), string.Empty);

        var user = new KciUser
        {
            UserName = userName,
            Email = userName,
            Password = password 
        };
        _context.KciUsers.Add(user);
        await _context.SaveChangesAsync(CancellationToken.None);
        return (Result.Success(), user.Id.ToString());
    }

    public async Task<Result> DeleteUserAsync(string userId)
    {
        if (!int.TryParse(userId, out var id))
            return Result.Failure(new[] { "Invalid user id" });
        var user = await _context.KciUsers.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
            return Result.Success();
        _context.KciUsers.Remove(user);
        await _context.SaveChangesAsync(CancellationToken.None);
        return Result.Success();
    }
}
