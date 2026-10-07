using MediatR;

namespace PetSitting.Application.Features.Pets.GetPets;

public record GetPetsQuery(int UserId) : IRequest<PetResponse>;
