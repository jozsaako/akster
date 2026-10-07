namespace PetSitting.Domain.Pets;

/// <summary>
/// PetPicture value object representing a single picture of a pet.
/// </summary>
public class PetPicture
{
    public int Id { get; set; }
    public int PetId { get; set; }
    public Pet Pet { get; set; } = null!;
    public string Url { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}
