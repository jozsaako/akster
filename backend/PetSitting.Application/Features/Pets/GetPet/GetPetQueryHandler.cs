using MediatR;
using PetSitting.Application.Abstractions.Repositories;

namespace PetSitting.Application.Features.Pets.GetPet;

public class GetPetQueryHandler : IRequestHandler<GetPetQuery, PetResponse>
{
    private readonly IPetRepository _pets;

    public GetPetQueryHandler(IPetRepository pets)
    {
        _pets = pets;
    }

    public async Task<PetResponse> Handle(GetPetQuery request, CancellationToken cancellationToken)
    {
        var pet = await _pets.GetByIdAsync(request.PetId, cancellationToken);
        return pet == null || pet.UserId != request.UserId
            ? new PetResponse(false, "Pet not found.")
            : new PetResponse(true, string.Empty, pet.ToDto());
    }
}
