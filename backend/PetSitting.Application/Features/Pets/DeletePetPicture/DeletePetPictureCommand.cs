using MediatR;

namespace PetSitting.Application.Features.Pets.DeletePetPicture;

public record DeletePetPictureCommand(int UserId, int PetId, int PictureId) : IRequest<PetResponse>;
