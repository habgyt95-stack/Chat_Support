namespace Chat_Support.Application.Auth.DTOs;

public class AuthTokens
{
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }
}
