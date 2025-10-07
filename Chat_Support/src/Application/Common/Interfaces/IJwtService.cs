using System.Security.Claims;
using Chat_Support.Application.Auth.DTOs;
using Chat_Support.Domain.Entities;

namespace Chat_Support.Application.Common.Interfaces;

public interface IJwtService
{
    Task<AuthTokens> GenerateTokensAsync(KciUser user);
    ClaimsPrincipal GetPrincipalFromExpiredToken(string token);
}
