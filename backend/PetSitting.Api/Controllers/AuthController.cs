using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetSitting.Api.Contracts;
using PetSitting.Application.Users;

namespace PetSitting.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ApiControllerBase
{
    private readonly IUsersManager _usersManager;

    public AuthController(IUsersManager usersManager)
    {
        _usersManager = usersManager;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest r) =>
        ToAction(await _usersManager.LoginAsync(new LoginInput(r.Email, r.Password)), a => ToResponse("Login successful.", a));

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest r) =>
        ToAction(await _usersManager.RegisterAsync(new RegisterInput(r.Email, r.Password, r.FirstName, r.LastName)), a => ToResponse("Registration successful.", a));

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request) =>
        ToAction(await _usersManager.RefreshAsync(request.RefreshToken), a => ToResponse("Token refreshed.", a));

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request) =>
        ToAction(await _usersManager.LogoutAsync(request.RefreshToken),
            new AuthResponse(true, "Logged out (refresh token revoked)."));

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMe() =>
        ToAction(await _usersManager.GetCurrentAsync(UserId), user => new AuthResponse(true, string.Empty, user));

    [HttpPatch("me")]
    [Authorize]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest request)
    {
        var result = await _usersManager.UpdateProfileAsync(
            UserId, new UpdateProfileInput(request.FirstName, request.LastName, request.Email, request.Address));
        return ToAction(result, a => ToResponse("Profile updated.", a));
    }

    [HttpPost("me/avatar")]
    [Authorize]
    public async Task<IActionResult> UploadAvatar(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { Success = false, Message = "No file provided." });

        return ToAction(await _usersManager.UploadAvatarAsync(UserId, ToUpload(file)),
            user => new AuthResponse(true, "Avatar uploaded.", user));
    }

    [HttpPut("me/owner")]
    [Authorize]
    public async Task<IActionResult> SetOwner([FromBody] SetOwnerRequest request) =>
        ToAction(await _usersManager.SetOwnerAsync(UserId, request.IsOwner!.Value),
            user => new AuthResponse(true, "Updated.", user));

    private static AuthResponse ToResponse(string message, AuthResult a) =>
        new(true, message, a.User, a.Token, a.RefreshToken);
}
