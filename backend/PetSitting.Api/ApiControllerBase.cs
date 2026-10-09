using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using PetSitting.Application.Common;
using PetSitting.Domain;

namespace PetSitting.Api;

public abstract class ApiControllerBase : ControllerBase
{
    protected int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // 200 with the body built from the value, or the status for the failure kind.
    protected IActionResult ToAction<T>(Result<T> result, Func<T, object> body) =>
        result.Match<IActionResult>(value => Ok(body(value)), Fail);

    protected IActionResult ToAction(Result result, object body) =>
        result.Match<IActionResult>(() => Ok(body), Fail);

    protected IActionResult Fail(ErrorKind kind, string message)
    {
        var status = kind switch
        {
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
        return StatusCode(status, new { success = false, message });
    }

    protected static FileUpload ToUpload(IFormFile file) =>
        new(file.OpenReadStream(), file.FileName, file.ContentType, file.Length);
}
