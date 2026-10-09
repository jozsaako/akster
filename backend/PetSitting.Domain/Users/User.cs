using PetSitting.Domain.Pets;

namespace PetSitting.Domain.Users;

/// <summary>
/// User aggregate root. A user can be an owner (IsOwner) and, independently, a sitter (active SitterProfile).
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
    public DateOnly? DateOfBirth { get; set; }
    public bool IsOwner { get; set; } = true;
    public string? ProfilePictureUrl { get; set; }
    public Location? Location { get; private set; }

    public void SetLocation(Location location) => Location = location;

    // Relationships
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<Pet> Pets { get; set; } = new List<Pet>();
}
