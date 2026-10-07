using MediatR;
using PetSitting.Application.Abstractions.Repositories;

namespace PetSitting.Application.Features.Pets.DeletePet;

public class DeletePetCommandHandler : IRequestHandler<DeletePetCommand, PetResponse>
{
    private readonly IPetRepository _pets;

    public DeletePetCommandHandler(IPetRepository pets)
    {
        _pets = pets;
    }

    public async Task<PetResponse> Handle(DeletePetCommand request, CancellationToken cancellationToken)
    {
        var pet = await _pets.GetByIdAsync(request.PetId, cancellationToken);
        if (pet == null || pet.UserId != request.UserId)
        {
            return new PetResponse(false, "Pet not found.");
        }

        await _pets.DeleteAsync(pet.Id, cancellationToken);
        return new PetResponse(true, "Pet deleted.");
    }
}
