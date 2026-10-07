using MediatR;
using Microsoft.Extensions.Logging;
using PetSitting.Application.Abstractions;
using PetSitting.Application.Abstractions.Repositories;
using PetSitting.Domain.Identity;
using System.Security.Cryptography;
using System.Text;

namespace PetSitting.Application.Features.Identity.Register;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResponse>
{
    private readonly IUserRepository _users;
    private readonly IJwtTokenService _tokens;
    private readonly ILogger<RegisterCommandHandler> _logger;

    public RegisterCommandHandler(IUserRepository users, IJwtTokenService tokens, ILogger<RegisterCommandHandler> logger)
    {
        _users = users;
        _tokens = tokens;
        _logger = logger;
    }

    public async Task<AuthResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        if (await _users.GetByEmailAsync(request.Email, cancellationToken) != null)
        {
            return new AuthResponse(false, "User with this email already exists.");
        }

        var user = new User
        {
            Email = request.Email,
            PasswordHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(request.Password))),
            FirstName = request.FirstName,
            LastName = request.LastName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsEmailConfirmed = false,
            Role = UserRole.Owner
        };

        var refresh = _tokens.CreateRefreshToken();
        user.RefreshTokens.Add(refresh);
        await _users.AddAsync(user, cancellationToken);

        _logger.LogInformation("User registered successfully: {Email}", user.Email);

        return new AuthResponse(true, "Registration successful.", user.ToDto(), _tokens.GenerateJwt(user), refresh.Token);
    }
}
