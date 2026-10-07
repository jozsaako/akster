using MediatR;
using Microsoft.Extensions.Logging;
using PetSitting.Application.Abstractions;
using PetSitting.Application.Abstractions.Repositories;
using System.Security.Cryptography;
using System.Text;

namespace PetSitting.Application.Features.Identity.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponse>
{
    private readonly IUserRepository _users;
    private readonly IJwtTokenService _tokens;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(IUserRepository users, IJwtTokenService tokens, ILogger<LoginCommandHandler> logger)
    {
        _users = users;
        _tokens = tokens;
        _logger = logger;
    }

    public async Task<AuthResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByEmailAsync(request.Email, cancellationToken);
        if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
        {
            return new AuthResponse(false, "Invalid email or password.");
        }

        var refresh = _tokens.CreateRefreshToken();
        user.RefreshTokens.Add(refresh);
        await _users.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("User logged in successfully: {Email}", user.Email);

        return new AuthResponse(true, "Login successful.", user.ToDto(), _tokens.GenerateJwt(user), refresh.Token);
    }

    private static bool VerifyPassword(string password, string hash)
    {
        var hashOfInput = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return hashOfInput.SequenceEqual(Convert.FromBase64String(hash));
    }
}
