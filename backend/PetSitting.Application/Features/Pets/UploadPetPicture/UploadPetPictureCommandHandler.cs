using MediatR;
using PetSitting.Application.Abstractions;
using PetSitting.Application.Abstractions.Repositories;
using PetSitting.Domain.Pets;

namespace PetSitting.Application.Features.Pets.UploadPetPicture;

public class UploadPetPictureCommandHandler : IRequestHandler<UploadPetPictureCommand, PetResponse>
{
    private readonly IPetRepository _pets;
    private readonly IBlobService _blobs;

    public UploadPetPictureCommandHandler(IPetRepository pets, IBlobService blobs)
    {
        _pets = pets;
        _blobs = blobs;
    }

    public async Task<PetResponse> Handle(UploadPetPictureCommand request, CancellationToken cancellationToken)
    {
        var pet = await _pets.GetByIdAsync(request.PetId, cancellationToken);
        if (pet == null || pet.UserId != request.UserId)
        {
            return new PetResponse(false, "Pet not found.");
        }

        string url;
        try
        {
            url = await _blobs.UploadPetPictureAsync(request.UserId, pet.Id, request.File, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return new PetResponse(false, ex.Message);
        }

        pet.Pictures.Add(new PetPicture { Url = url, UploadedAt = DateTime.UtcNow });
        await _pets.UpdateAsync(pet, cancellationToken);

        return new PetResponse(true, "Picture uploaded.", pet.ToDto());
    }
}
