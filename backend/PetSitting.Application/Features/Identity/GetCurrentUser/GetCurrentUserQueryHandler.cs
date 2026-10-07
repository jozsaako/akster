using MediatR;
using PetSitting.Application.Abstractions.Repositories;

namespace PetSitting.Application.Features.Identity.GetCurrentUser;

public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, AuthResponse>
{
    private readonly IUserRepository _users;

    public GetCurrentUserQueryHandler(IUserRepository users)
    {
        _users = users;
    }

    public async Task<AuthResponse> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(request.UserId, cancellationToken);
        return user == null
            ? new AuthResponse(false, "User not found.")
            : new AuthResponse(true, string.Empty, user.ToDto());
    }
}
