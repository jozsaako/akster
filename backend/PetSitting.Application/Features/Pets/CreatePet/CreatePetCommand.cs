using MediatR;

namespace PetSitting.Application.Features.Pets.CreatePet;

public record CreatePetCommand(
    int UserId,
    string Name,
    int Age,
    string Gender,
    string Type,
    string? SpecialNeeds
) : IRequest<PetResponse>;
