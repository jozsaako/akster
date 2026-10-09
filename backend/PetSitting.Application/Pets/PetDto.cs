using PetSitting.Domain.Pets;

namespace PetSitting.Application.Pets;

public record PetDto(
    int Id,
    int UserId,
    string Name,
    int Age,
    string Gender,
    string Type,
    string? SpecialNeeds,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<PetPictureDto> Pictures
);

public record PetPictureDto(int Id, string Url, DateTime UploadedAt);

public static class PetMappings
{
    public static PetDto ToDto(this Pet p) => new(
        p.Id,
        p.UserId,
        p.Name,
        p.Age,
        p.Gender.ToString(),
        p.Type.ToString(),
        p.SpecialNeeds,
        p.CreatedAt,
        p.UpdatedAt,
        p.Pictures.Select(pic => new PetPictureDto(pic.Id, pic.Url, pic.UploadedAt)).ToList());
}
