using MediatR;

namespace PetSitting.Application.Features.Identity.Logout;

public record LogoutCommand(string RefreshToken) : IRequest<AuthResponse>;
