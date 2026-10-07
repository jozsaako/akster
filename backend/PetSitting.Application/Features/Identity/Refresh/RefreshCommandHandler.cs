using MediatR;
using PetSitting.Application.Abstractions;
using PetSitting.Application.Abstractions.Repositories;

namespace PetSitting.Application.Features.Identity.Refresh;

public class RefreshCommandHandler : IRequestHandler<RefreshCommand, AuthResponse>
{
    private readonly IUserRepository _users;
    private readonly IJwtTokenService _tokens;

    public RefreshCommandHandler(IUserRepository users, IJwtTokenService tokens)
    {
        _users = users;
        _tokens = tokens;
    }

    public async Task<AuthResponse> Handle(RefreshCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByRefreshTokenAsync(request.RefreshToken, cancellationToken);
        var stored = user?.RefreshTokens.FirstOrDefault(r => r.Token == request.RefreshToken);
        if (user == null || stored == null || stored.IsRevoked || stored.ExpiresAt <= DateTime.UtcNow)
        {
            return new AuthResponse(false, "Invalid or expired refresh token.");
        }

        stored.IsRevoked = true;
        var next = _tokens.CreateRefreshToken();
        user.RefreshTokens.Add(next);
        await _users.UpdateAsync(user, cancellationToken);

        return new AuthResponse(true, "Token refreshed.", user.ToDto(), _tokens.GenerateJwt(user), next.Token);
    }
}
