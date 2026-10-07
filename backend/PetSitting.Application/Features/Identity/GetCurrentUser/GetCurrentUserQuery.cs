using MediatR;

namespace PetSitting.Application.Features.Identity.GetCurrentUser;

public record GetCurrentUserQuery(int UserId) : IRequest<AuthResponse>;
