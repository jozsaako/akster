using MediatR;

namespace PetSitting.Application.Features.Pets.DeletePet;

public record DeletePetCommand(int UserId, int PetId) : IRequest<PetResponse>;
