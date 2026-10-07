using MediatR;
using PetSitting.Application.Abstractions;

namespace PetSitting.Application.Features.Pets.UploadPetPicture;

public record UploadPetPictureCommand(int UserId, int PetId, FileUpload File) : IRequest<PetResponse>;
