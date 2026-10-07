using MediatR;

namespace PetSitting.Application.Features.Identity.ChangeRole;

public record ChangeRoleCommand(int UserId, string Role) : IRequest<AuthResponse>;
