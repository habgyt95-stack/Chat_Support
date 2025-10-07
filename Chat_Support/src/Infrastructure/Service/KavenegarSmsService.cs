using Chat_Support.Application.Common.Interfaces;
using Kavenegar;
using Microsoft.Extensions.Configuration;

namespace Chat_Support.Infrastructure.Service;

public class KavenegarSmsService : ISmsService
{
    private readonly KavenegarApi _api;

    public KavenegarSmsService(IConfiguration configuration)
    {
        _api = new KavenegarApi(configuration["Kavenegar:ApiKey"]);
    }

    public async Task SendOtpAsync(string phoneNumber, string code)
    {
        await _api.VerifyLookup(phoneNumber, code, "Verify");
    }
}
