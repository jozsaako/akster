using MediatR;

namespace PetSitting.Application.Features.Identity.Refresh;

public record RefreshCommand(string RefreshToken) : IRequest<AuthResponse>;
