namespace PetSitting.Application.Availability;

public record UpsertAvailabilityInput(
    Dictionary<string, List<string>> Schedule,
    List<string> Services,
    List<string> AcceptedPetTypes,
    int MaxPets,
    string? Bio
);
