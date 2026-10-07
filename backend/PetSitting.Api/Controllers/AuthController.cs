using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetSitting.Api.Contracts;
using PetSitting.Application.Features.Identity.ChangeRole;
using PetSitting.Application.Features.Identity.GetCurrentUser;
using PetSitting.Application.Features.Identity.Login;
using PetSitting.Application.Features.Identity.Logout;
using PetSitting.Application.Features.Identity.Refresh;
using PetSitting.Application.Features.Identity.Register;
using PetSitting.Application.Features.Identity.UpdateProfile;
using PetSitting.Application.Features.Identity.UploadAvatar;

namespace PetSitting.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        var result = await _mediator.Send(new RefreshCommand(request.RefreshToken));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request)
    {
        var result = await _mediator.Send(new LogoutCommand(request.RefreshToken));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMe()
    {
        var result = await _mediator.Send(new GetCurrentUserQuery(UserId));
        return Respond(result.Success, result, result.Message);
    }

    [HttpPatch("me")]
    [Authorize]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest request)
    {
        var result = await _mediator.Send(
            new UpdateProfileCommand(UserId, request.FirstName, request.LastName, request.Email, request.Address));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("me/avatar")]
    [Authorize]
    public async Task<IActionResult> UploadAvatar(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { Success = false, Message = "No file provided." });

        var result = await _mediator.Send(new UploadAvatarCommand(UserId, ToUpload(file)));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("change-role")]
    [Authorize]
    public async Task<IActionResult> ChangeRole([FromBody] ChangeRoleRequest request)
    {
        var result = await _mediator.Send(new ChangeRoleCommand(UserId, request.Role));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
