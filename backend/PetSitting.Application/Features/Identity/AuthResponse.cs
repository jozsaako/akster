using PetSitting.Domain.Identity;

namespace PetSitting.Application.Features.Identity;

public record AuthResponse(
    bool Success,
    string Message,
    UserDto? User = null,
    string? Token = null,
    string? RefreshToken = null
);

public record UserDto(
    int Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    string? ProfilePictureUrl,
    string? Address
);

public static class UserMappings
{
    public static UserDto ToDto(this User u) =>
        new(u.Id, u.Email, u.FirstName, u.LastName, u.Role.ToString(), u.ProfilePictureUrl, u.Address);
}
