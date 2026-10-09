using PetSitting.Domain.Users;

namespace PetSitting.Application.Users;

/// <summary>The user's own location (street included). Coordinates are null for a not-yet-structured legacy address.</summary>
public record LocationDto(
    string? County,
    string? City,
    string? PostalCode,
    string? Street,
    double? Latitude,
    double? Longitude
);

public record UserDto(
    int Id,
    string Email,
    string FirstName,
    string LastName,
    DateOnly? DateOfBirth,
    bool IsOwner,
    string? ProfilePictureUrl,
    LocationDto? Location
);

/// <summary>A user plus freshly issued tokens (login, register, refresh, profile changes).</summary>
public record AuthResult(UserDto User, string? Token = null, string? RefreshToken = null);

public static class UserMappings
{
    public static UserDto ToDto(this User u) =>
        new(u.Id, u.Email, u.FirstName, u.LastName, u.DateOfBirth, u.IsOwner, u.ProfilePictureUrl,
            u.Location is { } l ? new LocationDto(l.County, l.City, l.PostalCode, l.Street, l.Latitude, l.Longitude) : null);
}
