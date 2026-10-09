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
    Task<Result<UserDto>> SetOwnerAsync(int userId, bool isOwner, CancellationToken cancellationToken = default);

    Task<Result<UserDto>> UpdateLocationAsync(int userId, UpdateLocationInput input, CancellationToken cancellationToken = default);
    IReadOnlyList<string> GetCounties();

    /// <summary>For other subsystems: has this user saved a geocoded location?</summary>
    Task<bool> HasLocationAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>For other subsystems: does this user exist?</summary>
    Task<bool> ExistsAsync(int userId, CancellationToken cancellationToken = default);
}
