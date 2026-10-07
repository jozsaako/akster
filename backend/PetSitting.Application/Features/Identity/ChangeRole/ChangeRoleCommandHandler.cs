using MediatR;
using PetSitting.Application.Abstractions;
using PetSitting.Application.Abstractions.Repositories;
using PetSitting.Domain.Identity;

namespace PetSitting.Application.Features.Identity.ChangeRole;

public class ChangeRoleCommandHandler : IRequestHandler<ChangeRoleCommand, AuthResponse>
{
    private readonly IUserRepository _users;
    private readonly IJwtTokenService _tokens;

    public ChangeRoleCommandHandler(IUserRepository users, IJwtTokenService tokens)
    {
        _users = users;
        _tokens = tokens;
    }

    public async Task<AuthResponse> Handle(ChangeRoleCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            return new AuthResponse(false, "User not found.");
        }

        if (!Enum.TryParse<UserRole>(request.Role, true, out var role))
        {
            return new AuthResponse(false, "Invalid role. Valid roles are: Owner, Sitter.");
        }

        user.Role = role;
        user.UpdatedAt = DateTime.UtcNow;
        await _users.UpdateAsync(user, cancellationToken);

        return new AuthResponse(true, "Role changed successfully.", user.ToDto(), _tokens.GenerateJwt(user));
    }
}
