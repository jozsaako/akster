using PetSitting.Domain.Identity;

namespace PetSitting.Domain.Pets;

/// <summary>
/// Pet aggregate root. Represents a pet owned by a user.
/// </summary>
public class Pet
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public PetGender Gender { get; set; }
    public PetType Type { get; set; }
    public string? SpecialNeeds { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Child entity
    public ICollection<PetPicture> Pictures { get; set; } = new List<PetPicture>();
}
