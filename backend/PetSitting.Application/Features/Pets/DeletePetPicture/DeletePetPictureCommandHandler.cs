using MediatR;
using PetSitting.Application.Abstractions.Repositories;

namespace PetSitting.Application.Features.Pets.DeletePetPicture;

public class DeletePetPictureCommandHandler : IRequestHandler<DeletePetPictureCommand, PetResponse>
{
    private readonly IPetRepository _pets;

    public DeletePetPictureCommandHandler(IPetRepository pets)
    {
        _pets = pets;
    }

    public async Task<PetResponse> Handle(DeletePetPictureCommand request, CancellationToken cancellationToken)
    {
        var pet = await _pets.GetByIdAsync(request.PetId, cancellationToken);
        if (pet == null || pet.UserId != request.UserId)
        {
            return new PetResponse(false, "Pet not found.");
        }

        var picture = pet.Pictures.FirstOrDefault(p => p.Id == request.PictureId);
        if (picture == null)
        {
            return new PetResponse(false, "Picture not found.");
        }

        pet.Pictures.Remove(picture);
        await _pets.UpdateAsync(pet, cancellationToken);

        return new PetResponse(true, "Picture deleted.", pet.ToDto());
    }
}
