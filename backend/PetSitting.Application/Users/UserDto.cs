using PetSitting.Domain.Users;

namespace PetSitting.Application.Users;

public record UserDto(
    int Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    string? ProfilePictureUrl,
    string? Address
);

/// <summary>A user plus freshly issued tokens (login, register, refresh, profile and role changes).</summary>
public record AuthResult(UserDto User, string? Token = null, string? RefreshToken = null);

public static class UserMappings
{
    public static UserDto ToDto(this User u) =>
        new(u.Id, u.Email, u.FirstName, u.LastName, u.Role.ToString(), u.ProfilePictureUrl, u.Address);
}
