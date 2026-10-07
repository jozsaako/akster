using MediatR;

namespace PetSitting.Application.Features.Identity.Register;

public record RegisterCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName
) : IRequest<AuthResponse>;
