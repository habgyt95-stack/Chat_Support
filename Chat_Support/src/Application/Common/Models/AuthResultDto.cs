namespace Chat_Support.Application.Common.Models;

public record AuthResultDto(
    string AccessToken,
    string RefreshToken
);
