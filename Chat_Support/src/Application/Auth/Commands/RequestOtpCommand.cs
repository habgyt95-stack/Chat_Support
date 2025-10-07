using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Domain.Entities;
namespace Chat_Support.Application.Auth.Commands;

public record RequestOtpResult(bool Sent, int? VerifyNumber);

public record RequestOtpCommand(string Mobile) : IRequest<RequestOtpResult>;

public class RequestOtpCommandHandler : IRequestHandler<RequestOtpCommand, RequestOtpResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ISmsService _smsService;

    public RequestOtpCommandHandler(IApplicationDbContext context, ISmsService smsService)
    {
        _context = context;
        _smsService = smsService;
    }

    public async Task<RequestOtpResult> Handle(RequestOtpCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.KciUsers.FirstOrDefaultAsync(u => u.UserName == request.Mobile && u.Enable == true, cancellationToken: cancellationToken);
        if (user == null) return new RequestOtpResult(false, null);

        var lastOtp = await _context.ChatLoginOtps
            .Where(o => o.UserId == user.Id)
            .OrderByDescending(o => o.CreationDate)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);

        //if (lastOtp != null && lastOtp.CreationDate > DateTime.Now.AddMinutes(-2))
        //{
        //    return new RequestOtpResult(false, null);
        //}

        // Generate a 4-digit code
        var rnd = new Random();
        var code = rnd.Next(1000, 9999);

        var otp = new ChatLoginOtp
        {
            UserId = user.Id,
            CodeHash = BCrypt.Net.BCrypt.HashPassword(code.ToString()),
            ExpirationTime = DateTime.Now.AddMinutes(2),
        };

        await _context.ChatLoginOtps.AddAsync(otp, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await _smsService.SendOtpAsync(user.Mobile, code.ToString());

        return new RequestOtpResult(true, code);
    }
}
