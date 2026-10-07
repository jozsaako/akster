using MediatR;

namespace PetSitting.Application.Features.Pets.GetPet;

public record GetPetQuery(int UserId, int PetId) : IRequest<PetResponse>;
