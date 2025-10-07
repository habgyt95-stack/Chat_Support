using Chat_Support.Application.Auth.DTOs;
using Chat_Support.Application.Common.Interfaces;

namespace Chat_Support.Application.Auth.Commands;

public record VerifyOtpCommand(string Mobile, string Code) : IRequest<AuthTokens>;

public class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, AuthTokens>
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtService _jwtService;

    public VerifyOtpCommandHandler(IApplicationDbContext context, IJwtService jwtService)
    {
        _context = context;
        _jwtService = jwtService;
    }

    public async Task<AuthTokens> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.KciUsers.FirstOrDefaultAsync(u => u.UserName == request.Mobile, cancellationToken: cancellationToken);
        if (user == null) return null!;

        var otp = await _context.ChatLoginOtps
            .Where(o => o.UserId == user.Id && !o.IsUsed && o.ExpirationTime > DateTime.Now)
            .OrderByDescending(o => o.CreationDate)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);

        if (otp is not { Attempts: < 5 }) return null!;

        var code = request.Code?.Trim();
        if (string.IsNullOrEmpty(code) || !BCrypt.Net.BCrypt.Verify(code, otp.CodeHash))
        {
            otp.Attempts++;
            await _context.SaveChangesAsync(cancellationToken: cancellationToken);
            return null!;
        }

        otp.IsUsed = true;
        await _context.SaveChangesAsync(cancellationToken: cancellationToken);

        // Revert: throw if token generation fails to surface errors explicitly
        return await _jwtService.GenerateTokensAsync(user) ?? throw new NullReferenceException("Failed to generate JWT tokens");
    }
}
