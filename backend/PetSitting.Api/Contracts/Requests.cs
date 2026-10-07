namespace PetSitting.Api.Contracts;

public record RefreshRequest(string RefreshToken);
public record ChangeRoleRequest(string Role);
public record UpdateProfileRequest(string FirstName, string LastName, string Email, string? Address);
public record PetRequest(string Name, int Age, string Gender, string Type, string? SpecialNeeds);
public record AvailabilityRequest(
    Dictionary<string, List<string>> Schedule,
    List<string> Services,
    List<string> AcceptedPetTypes,
    int MaxPets,
    string? Bio);
