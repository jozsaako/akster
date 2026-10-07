using MediatR;

namespace PetSitting.Application.Features.Identity.Login;

public record LoginCommand(string Email, string Password) : IRequest<AuthResponse>;
