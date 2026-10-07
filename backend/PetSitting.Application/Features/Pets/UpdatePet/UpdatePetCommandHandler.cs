using MediatR;
using PetSitting.Application.Abstractions.Repositories;
using PetSitting.Domain.Pets;

namespace PetSitting.Application.Features.Pets.UpdatePet;

public class UpdatePetCommandHandler : IRequestHandler<UpdatePetCommand, PetResponse>
{
    private readonly IPetRepository _pets;

    public UpdatePetCommandHandler(IPetRepository pets)
    {
        _pets = pets;
    }

    public async Task<PetResponse> Handle(UpdatePetCommand request, CancellationToken cancellationToken)
    {
        var pet = await _pets.GetByIdAsync(request.PetId, cancellationToken);
        if (pet == null || pet.UserId != request.UserId)
        {
            return new PetResponse(false, "Pet not found.");
        }

        // validator guarantees these parse
        pet.Name = request.Name.Trim();
        pet.Age = request.Age;
        pet.Gender = Enum.Parse<PetGender>(request.Gender, true);
        pet.Type = Enum.Parse<PetType>(request.Type, true);
        pet.SpecialNeeds = request.SpecialNeeds?.Trim();
        pet.UpdatedAt = DateTime.UtcNow;

        await _pets.UpdateAsync(pet, cancellationToken);
        return new PetResponse(true, "Pet updated.", pet.ToDto());
    }
}
