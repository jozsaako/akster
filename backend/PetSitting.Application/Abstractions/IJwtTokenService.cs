using PetSitting.Domain.Identity;

namespace PetSitting.Application.Abstractions;

public interface IJwtTokenService
{
    string? GenerateJwt(User user);
    RefreshToken CreateRefreshToken();
}
