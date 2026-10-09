using System.Text.Json;
using PetSitting.Domain.Availability;

namespace PetSitting.Application.Availability;

public record AvailabilityDto(
    int Id,
    int UserId,
    Dictionary<string, List<string>> Schedule,
    List<string> Services,
    List<string> AcceptedPetTypes,
    int MaxPets,
    string? Bio,
    DateTime UpdatedAt
);

public static class AvailabilityMappings
{
    public static AvailabilityDto ToDto(this SitterAvailability a) => new(
        a.Id,
        a.UserId,
        JsonSerializer.Deserialize<Dictionary<string, List<string>>>(a.ScheduleJson) ?? new(),
        JsonSerializer.Deserialize<List<string>>(a.ServicesJson) ?? new(),
        JsonSerializer.Deserialize<List<string>>(a.AcceptedPetTypesJson) ?? new(),
        a.MaxPets,
        a.Bio,
        a.UpdatedAt);
}
