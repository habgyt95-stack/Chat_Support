namespace Chat_Support.Application.Common.Interfaces;

public interface ISmsService
{
    Task SendOtpAsync(string phoneNumber, string code);
}
