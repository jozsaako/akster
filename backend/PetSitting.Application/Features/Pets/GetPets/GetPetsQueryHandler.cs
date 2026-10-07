using MediatR;
using PetSitting.Application.Abstractions.Repositories;

namespace PetSitting.Application.Features.Pets.GetPets;

public class GetPetsQueryHandler : IRequestHandler<GetPetsQuery, PetResponse>
{
    private readonly IPetRepository _pets;

    public GetPetsQueryHandler(IPetRepository pets)
    {
        _pets = pets;
    }

    public async Task<PetResponse> Handle(GetPetsQuery request, CancellationToken cancellationToken)
    {
        var pets = await _pets.GetByUserIdAsync(request.UserId, cancellationToken);
        return new PetResponse(true, string.Empty, Pets: pets.Select(p => p.ToDto()).ToList());
    }
}
