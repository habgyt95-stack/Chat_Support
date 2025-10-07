using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Chat_Support.Application.Auth.DTOs;
using Chat_Support.Application.Common.Interfaces;
using Chat_Support.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Chat_Support.Infrastructure.Service;

public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;
    private readonly IApplicationDbContext _context; // DbContext خود را اینجا تزریق کنید

    public JwtService(IConfiguration configuration, IApplicationDbContext context)
    {
        _configuration = configuration;
        _context = context;
    }

    public async Task<AuthTokens> GenerateTokensAsync(KciUser user)
    {
        var accessToken = GenerateAccessToken(user);
        var refreshToken = await GenerateAndStoreRefreshTokenAsync(user.Id);

        return new AuthTokens { AccessToken = accessToken, RefreshToken = refreshToken };
    }

    private string GenerateAccessToken(KciUser user)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtChat:Key"] ?? string.Empty));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName),
            new Claim("firstname", user.FirstName ?? ""),
            new Claim("lastname", user.LastName ?? ""),
            new Claim("regionId", user.RegionId.ToString() ?? ""),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "User") 
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["JwtChat:Issuer"],
            audience: _configuration["JwtChat:Audience"],
            claims: claims,
            expires: DateTime.Now.AddMinutes(Convert.ToDouble(_configuration["JwtChat:AccessTokenExpirationMinutes"])),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<string> GenerateAndStoreRefreshTokenAsync(int userId)
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        var refreshTokenValue = Convert.ToBase64String(randomNumber);

        var refreshToken = new ChatUserRefreshToken()
        {
            UserId = userId,
            Token = refreshTokenValue,
            ExpirationTime = DateTime.Now.AddDays(Convert.ToDouble(_configuration["JwtChat:RefreshTokenExpirationDays"]))
        };

        await _context.ChatUserRefreshTokens.AddAsync(refreshToken);
        await _context.SaveChangesAsync(CancellationToken.None);

        return refreshTokenValue;
    }

    public ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
    {
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtChat:Key"] ?? string.Empty)),
            ValidateLifetime = false // اینجا مهم است
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);
        if (securityToken is not JwtSecurityToken jwtSecurityToken || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            throw new SecurityTokenException("Invalid token");

        return principal;
    }
}
