using System.Security.Claims;
using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Application.Common.Models;
using Chat_Support.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace Chat_Support.Application.Auth.Commands;

public record AuthenticateFromAppCommand : IRequest<AuthResultDto>;

public class AuthenticateFromAppCommandHandler : IRequestHandler<AuthenticateFromAppCommand, AuthResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtService _jwtTokenGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthenticateFromAppCommandHandler(
        IApplicationDbContext context,
        IJwtService jwtTokenGenerator,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _jwtTokenGenerator = jwtTokenGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<AuthResultDto> Handle(AuthenticateFromAppCommand request, CancellationToken cancellationToken)
    {
        var userPrincipal = _httpContextAccessor.HttpContext?.User;
        if (userPrincipal is null)
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        var userIdString = userPrincipal.FindFirstValue(ClaimTypes.NameIdentifier); 
        var deviceId = userPrincipal.FindFirstValue("deviceId");

        if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out var userId) || string.IsNullOrEmpty(deviceId))
        {
            throw new InvalidOperationException("Required claims are missing from the token.");
        }

        var user = await _context.KciUsers.FindAsync(userId);
        if (user == null) return null!;

        var tokens = await _jwtTokenGenerator.GenerateTokensAsync(user);
        var accessToken = tokens.AccessToken;
        var newRefreshTokenValue = tokens.RefreshToken;

        var refreshTokenEntity = new ChatUserRefreshToken
        {
            UserId = userId,
            CreationDate = DateTime.Now,
            Token = newRefreshTokenValue,
            ExpirationTime= DateTime.Now.AddDays(30), 
            IsRevoked = false,
        };

        await _context.ChatUserRefreshTokens.AddAsync(refreshTokenEntity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

       
        return new AuthResultDto(accessToken, newRefreshTokenValue);
    }
}
