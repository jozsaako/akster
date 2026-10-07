using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using PetSitting.Application.Abstractions;

namespace PetSitting.Api;

public abstract class ApiControllerBase : ControllerBase
{
    protected int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // 200 on success; otherwise 404 for "not found" messages and 400 for everything else.
    protected IActionResult Respond(bool success, object response, string message)
    {
        if (success) return Ok(response);
        return message.Contains("not found", StringComparison.OrdinalIgnoreCase) ? NotFound(response) : BadRequest(response);
    }

    protected static FileUpload ToUpload(IFormFile file) =>
        new(file.OpenReadStream(), file.FileName, file.ContentType, file.Length);
}
