using PetSitting.Application.Common;
using PetSitting.Domain;

namespace PetSitting.Application.Users;

public interface IUsersManager
{
    Task<Result<AuthResult>> LoginAsync(LoginInput input, CancellationToken cancellationToken = default);
    Task<Result<AuthResult>> RegisterAsync(RegisterInput input, CancellationToken cancellationToken = default);
    Task<Result<AuthResult>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<Result> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<Result<UserDto>> GetCurrentAsync(int userId, CancellationToken cancellationToken = default);
    Task<Result<AuthResult>> UpdateProfileAsync(int userId, UpdateProfileInput input, CancellationToken cancellationToken = default);
    Task<Result<UserDto>> UploadAvatarAsync(int userId, FileUpload file, CancellationToken cancellationToken = default);
    Task<Result<AuthResult>> ChangeRoleAsync(int userId, string role, CancellationToken cancellationToken = default);

    /// <summary>For other subsystems: does this user exist?</summary>
    Task<bool> ExistsAsync(int userId, CancellationToken cancellationToken = default);
}
