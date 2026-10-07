using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PetSitting.Application.Abstractions;
using PetSitting.Domain.Identity;

namespace PetSitting.Infrastructure.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfigurationSection _jwt;

    public JwtTokenService(IConfiguration configuration)
    {
        _jwt = configuration.GetSection("Jwt");
    }

    public string? GenerateJwt(User user)
    {
        var key = _jwt["Key"];
        if (string.IsNullOrEmpty(key)) return null;

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.GivenName, user.FirstName),
            new Claim(ClaimTypes.Surname, user.LastName),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _jwt["Issuer"],
            audience: _jwt["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwt.GetValue<int?>("ExpiresMinutes") ?? 60),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public RefreshToken CreateRefreshToken() => new()
    {
        Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
        ExpiresAt = DateTime.UtcNow.AddDays(_jwt.GetValue<int?>("RefreshTokenExpiresDays") ?? 7),
        IsRevoked = false
    };
}
