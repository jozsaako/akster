using System.Text.Json;
using PetSitting.Domain.Availability;

namespace PetSitting.Application.Availability;

public record AvailabilityDto(
    int Id,
    int UserId,
    bool IsActive,
    Dictionary<string, List<string>> Schedule,
    List<string> Services,
    List<string> AcceptedPetTypes,
    int MaxPets,
    string? Bio,
    DateTime UpdatedAt
);

public static class AvailabilityMappings
{
    public static AvailabilityDto ToDto(this SitterProfile a) => new(
        a.Id,
        a.UserId,
        a.IsActive,
        JsonSerializer.Deserialize<Dictionary<string, List<string>>>(a.ScheduleJson) ?? new(),
        JsonSerializer.Deserialize<List<string>>(a.ServicesJson) ?? new(),
        JsonSerializer.Deserialize<List<string>>(a.AcceptedPetTypesJson) ?? new(),
        a.MaxPets,
        a.Bio,
        a.UpdatedAt);
}
