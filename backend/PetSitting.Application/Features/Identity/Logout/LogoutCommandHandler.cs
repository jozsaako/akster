using MediatR;
using PetSitting.Application.Abstractions.Repositories;

namespace PetSitting.Application.Features.Identity.Logout;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, AuthResponse>
{
    private readonly IUserRepository _users;

    public LogoutCommandHandler(IUserRepository users)
    {
        _users = users;
    }

    public async Task<AuthResponse> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByRefreshTokenAsync(request.RefreshToken, cancellationToken);
        var stored = user?.RefreshTokens.FirstOrDefault(r => r.Token == request.RefreshToken);
        if (user == null || stored == null || stored.IsRevoked)
        {
            return new AuthResponse(false, "Invalid refresh token.");
        }

        stored.IsRevoked = true;
        await _users.UpdateAsync(user, cancellationToken);
        return new AuthResponse(true, "Logged out (refresh token revoked).");
    }
}
