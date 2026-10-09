using PetSitting.Domain.Availability;
using PetSitting.Domain.Pets;

namespace PetSitting.Domain.Users;

/// <summary>
/// User aggregate root. Represents both Pet Owners and Sitters.
/// </summary>
public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsEmailConfirmed { get; set; }
    public UserRole Role { get; set; } = UserRole.Owner;
    public string? ProfilePictureUrl { get; set; }
    public string? Address { get; set; }

    // Relationships
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<Pet> Pets { get; set; } = new List<Pet>();
    public SitterAvailability? SitterAvailability { get; set; }
}
