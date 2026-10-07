using MediatR;
using PetSitting.Application.Abstractions;
using PetSitting.Application.Abstractions.Repositories;

namespace PetSitting.Application.Features.Identity.UploadAvatar;

public class UploadAvatarCommandHandler : IRequestHandler<UploadAvatarCommand, AuthResponse>
{
    private readonly IUserRepository _users;
    private readonly IBlobService _blobs;

    public UploadAvatarCommandHandler(IUserRepository users, IBlobService blobs)
    {
        _users = users;
        _blobs = blobs;
    }

    public async Task<AuthResponse> Handle(UploadAvatarCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            return new AuthResponse(false, "User not found.");
        }

        try
        {
            user.ProfilePictureUrl = await _blobs.UploadAvatarAsync(user.Id, request.File, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return new AuthResponse(false, ex.Message);
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _users.UpdateAsync(user, cancellationToken);

        return new AuthResponse(true, "Avatar uploaded.", user.ToDto());
    }
}
