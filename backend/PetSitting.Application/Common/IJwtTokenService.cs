using PetSitting.Domain.Users;

namespace PetSitting.Application.Common;

public interface IJwtTokenService
{
    string? GenerateJwt(User user);
    RefreshToken CreateRefreshToken();
}
