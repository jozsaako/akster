using MediatR;
using PetSitting.Application.Abstractions;

namespace PetSitting.Application.Features.Identity.UploadAvatar;

public record UploadAvatarCommand(int UserId, FileUpload File) : IRequest<AuthResponse>;
