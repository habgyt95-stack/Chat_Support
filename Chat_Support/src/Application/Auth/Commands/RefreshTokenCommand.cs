using Chat_Support.Application.Auth.DTOs;
using Chat_Support.Application.Common.Interfaces;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Chat_Support.Application.Auth.Commands;

public record RefreshTokenCommand(string AccessToken, string RefreshToken) : IRequest<AuthTokens>;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthTokens>
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtService _jwtService;

    public RefreshTokenCommandHandler(IApplicationDbContext context, IJwtService jwtService)
    {
        _context = context;
        _jwtService = jwtService;
    }

    public async Task<AuthTokens> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var principal = _jwtService.GetPrincipalFromExpiredToken(request.AccessToken);
        var userId = int.Parse(principal.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);

        var savedRefreshToken = await _context.ChatUserRefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken && rt.UserId == userId && !rt.IsRevoked && rt.ExpirationTime > DateTime.Now, cancellationToken: cancellationToken);

        if (savedRefreshToken == null) return null!;

        var user = await _context.KciUsers.FindAsync(userId);
        if (user == null) return null!;

        // ابطال توکن رفرش قدیمی و صدور توکن‌های جدید
        savedRefreshToken.IsRevoked = true;
        await _context.SaveChangesAsync(cancellationToken: cancellationToken);

        return await _jwtService.GenerateTokensAsync(user);
    }
}
