using MediatR;
using PetSitting.Application.Abstractions;
using PetSitting.Application.Abstractions.Repositories;

namespace PetSitting.Application.Features.Identity.UpdateProfile;

public class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, AuthResponse>
{
    private readonly IUserRepository _users;
    private readonly IJwtTokenService _tokens;

    public UpdateProfileCommandHandler(IUserRepository users, IJwtTokenService tokens)
    {
        _users = users;
        _tokens = tokens;
    }

    public async Task<AuthResponse> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            return new AuthResponse(false, "User not found.");
        }

        var email = request.Email.Trim();
        var existing = await _users.GetByEmailAsync(email, cancellationToken);
        if (existing != null && existing.Id != user.Id)
        {
            return new AuthResponse(false, "Email is already in use.");
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.Email = email;
        user.Address = request.Address?.Trim();
        user.UpdatedAt = DateTime.UtcNow;
        await _users.UpdateAsync(user, cancellationToken);

        return new AuthResponse(true, "Profile updated.", user.ToDto(), _tokens.GenerateJwt(user));
    }
}
