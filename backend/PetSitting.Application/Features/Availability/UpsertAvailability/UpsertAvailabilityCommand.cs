using MediatR;

namespace PetSitting.Application.Features.Availability.UpsertAvailability;

public record UpsertAvailabilityCommand(
    int UserId,
    Dictionary<string, List<string>> Schedule,
    List<string> Services,
    List<string> AcceptedPetTypes,
    int MaxPets,
    string? Bio
) : IRequest<AvailabilityResponse>;
