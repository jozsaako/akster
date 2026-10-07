using MediatR;

namespace PetSitting.Application.Features.Identity.UpdateProfile;

public record UpdateProfileCommand(
    int UserId,
    string FirstName,
    string LastName,
    string Email,
    string? Address
) : IRequest<AuthResponse>;
