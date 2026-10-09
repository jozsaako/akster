using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using PetSitting.Application.Common;
using PetSitting.Domain;
using PetSitting.Domain.Users;

namespace PetSitting.Application.Users;

public class UsersManager : IUsersManager
{
    private const string UserNotFound = "User not found.";

    private readonly IUserRepository _usersRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IBlobService _blobService;
    private readonly ILogger<UsersManager> _logger;

    public UsersManager(
        IUserRepository usersRepository,
        IJwtTokenService jwtTokenService,
        IBlobService blobService,
        ILogger<UsersManager> logger)
    {
        _usersRepository = usersRepository;
        _jwtTokenService = jwtTokenService;
        _blobService = blobService;
        _logger = logger;
    }

    public async Task<Result<AuthResult>> LoginAsync(LoginInput input, CancellationToken cancellationToken = default)
    {
        var user = await _usersRepository.GetByEmailAsync(input.Email, cancellationToken);
        if (user == null || !VerifyPassword(input.Password, user.PasswordHash))
            return Result<AuthResult>.Fail(ErrorKind.Unauthorized, "Invalid email or password.");

        var refresh = _jwtTokenService.CreateRefreshToken();
        user.RefreshTokens.Add(refresh);
        await _usersRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("User logged in successfully: {Email}", user.Email);

        return Result<AuthResult>.Ok(new AuthResult(user.ToDto(), _jwtTokenService.GenerateJwt(user), refresh.Token));
    }

    public async Task<Result<AuthResult>> RegisterAsync(RegisterInput input, CancellationToken cancellationToken = default)
    {
        if (await _usersRepository.GetByEmailAsync(input.Email, cancellationToken) != null)
            return Result<AuthResult>.Fail(ErrorKind.Conflict, "User with this email already exists.");

        var user = new User
        {
            Email = input.Email,
            PasswordHash = HashPassword(input.Password),
            FirstName = input.FirstName,
            LastName = input.LastName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsEmailConfirmed = false,
            Role = UserRole.Owner
        };

        var refresh = _jwtTokenService.CreateRefreshToken();
        user.RefreshTokens.Add(refresh);
        await _usersRepository.AddAsync(user, cancellationToken);

        _logger.LogInformation("User registered successfully: {Email}", user.Email);

        return Result<AuthResult>.Ok(new AuthResult(user.ToDto(), _jwtTokenService.GenerateJwt(user), refresh.Token));
    }

    public async Task<Result<AuthResult>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Result<AuthResult>.Fail(ErrorKind.Validation, "Refresh token is required.");

        var user = await _usersRepository.GetByRefreshTokenAsync(refreshToken, cancellationToken);
        var stored = user?.RefreshTokens.FirstOrDefault(r => r.Token == refreshToken);
        if (user == null || stored == null || stored.IsRevoked || stored.ExpiresAt <= DateTime.UtcNow)
            return Result<AuthResult>.Fail(ErrorKind.Validation, "Invalid or expired refresh token.");

        stored.IsRevoked = true;
        var next = _jwtTokenService.CreateRefreshToken();
        user.RefreshTokens.Add(next);
        await _usersRepository.UpdateAsync(user, cancellationToken);

        return Result<AuthResult>.Ok(new AuthResult(user.ToDto(), _jwtTokenService.GenerateJwt(user), next.Token));
    }

    public async Task<Result> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Result.Fail(ErrorKind.Validation, "Refresh token is required.");

        var user = await _usersRepository.GetByRefreshTokenAsync(refreshToken, cancellationToken);
        var stored = user?.RefreshTokens.FirstOrDefault(r => r.Token == refreshToken);
        if (user == null || stored == null || stored.IsRevoked)
            return Result.Fail(ErrorKind.Validation, "Invalid refresh token.");

        stored.IsRevoked = true;
        await _usersRepository.UpdateAsync(user, cancellationToken);
        return Result.Ok();
    }

    public async Task<Result<UserDto>> GetCurrentAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _usersRepository.GetByIdAsync(userId, cancellationToken);
        return user == null
            ? Result<UserDto>.Fail(ErrorKind.NotFound, UserNotFound)
            : Result<UserDto>.Ok(user.ToDto());
    }

    public async Task<Result<AuthResult>> UpdateProfileAsync(int userId, UpdateProfileInput input, CancellationToken cancellationToken = default)
    {
        var user = await _usersRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null) return Result<AuthResult>.Fail(ErrorKind.NotFound, UserNotFound);

        var email = input.Email.Trim();
        var existing = await _usersRepository.GetByEmailAsync(email, cancellationToken);
        if (existing != null && existing.Id != user.Id)
            return Result<AuthResult>.Fail(ErrorKind.Conflict, "Email is already in use.");

        user.FirstName = input.FirstName.Trim();
        user.LastName = input.LastName.Trim();
        user.Email = email;
        user.Address = input.Address?.Trim();
        user.UpdatedAt = DateTime.UtcNow;
        await _usersRepository.UpdateAsync(user, cancellationToken);

        return Result<AuthResult>.Ok(new AuthResult(user.ToDto(), _jwtTokenService.GenerateJwt(user)));
    }

    public async Task<Result<UserDto>> UploadAvatarAsync(int userId, FileUpload file, CancellationToken cancellationToken = default)
    {
        var user = await _usersRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null) return Result<UserDto>.Fail(ErrorKind.NotFound, UserNotFound);

        try
        {
            user.ProfilePictureUrl = await _blobService.UploadAvatarAsync(user.Id, file, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return Result<UserDto>.Fail(ErrorKind.Validation, ex.Message);
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _usersRepository.UpdateAsync(user, cancellationToken);

        return Result<UserDto>.Ok(user.ToDto());
    }

    public async Task<Result<AuthResult>> ChangeRoleAsync(int userId, string role, CancellationToken cancellationToken = default)
    {
        var user = await _usersRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null) return Result<AuthResult>.Fail(ErrorKind.NotFound, UserNotFound);

        if (!Enum.TryParse<UserRole>(role, true, out var parsed))
            return Result<AuthResult>.Fail(ErrorKind.Validation, "Invalid role. Valid roles are: Owner, Sitter.");

        user.Role = parsed;
        user.UpdatedAt = DateTime.UtcNow;
        await _usersRepository.UpdateAsync(user, cancellationToken);

        return Result<AuthResult>.Ok(new AuthResult(user.ToDto(), _jwtTokenService.GenerateJwt(user)));
    }

    public async Task<bool> ExistsAsync(int userId, CancellationToken cancellationToken = default) =>
        await _usersRepository.GetByIdAsync(userId, cancellationToken) != null;

    // ponytail: unsalted SHA-256 kept as-is to avoid invalidating existing hashes; move to PasswordHasher with a migration path (docs 1.9 issue 1)
    private static string HashPassword(string password) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password)));

    private static bool VerifyPassword(string password, string hash) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(password)).SequenceEqual(Convert.FromBase64String(hash));
}
