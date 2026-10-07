using MediatR;

namespace PetSitting.Application.Features.Pets.UpdatePet;

public record UpdatePetCommand(
    int UserId,
    int PetId,
    string Name,
    int Age,
    string Gender,
    string Type,
    string? SpecialNeeds
) : IRequest<PetResponse>;
