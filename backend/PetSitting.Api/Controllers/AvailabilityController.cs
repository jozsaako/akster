using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetSitting.Api.Contracts;
using PetSitting.Application.Availability;

namespace PetSitting.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AvailabilityController : ApiControllerBase
{
    private readonly IAvailabilityManager _availabilityManager;

    public AvailabilityController(IAvailabilityManager availabilityManager)
    {
        _availabilityManager = availabilityManager;
    }

    [HttpGet]
    public async Task<IActionResult> GetAvailability() =>
        ToAction(await _availabilityManager.GetAsync(UserId), a => new AvailabilityResponse(true, string.Empty, a));

    [HttpPost("activate")]
    public async Task<IActionResult> Activate() =>
        ToAction(await _availabilityManager.ActivateAsync(UserId), a => new AvailabilityResponse(true, "Sitter profile activated.", a));

    [HttpPost("deactivate")]
    public async Task<IActionResult> Deactivate() =>
        ToAction(await _availabilityManager.DeactivateAsync(UserId), a => new AvailabilityResponse(true, "Sitter profile deactivated.", a));

    [HttpPut]
    public async Task<IActionResult> UpsertAvailability([FromBody] AvailabilityRequest r)
    {
        var result = await _availabilityManager.UpsertAsync(
            UserId, new UpsertAvailabilityInput(r.Schedule, r.Services, r.AcceptedPetTypes, r.MaxPets, r.Bio));
        return ToAction(result, a => new AvailabilityResponse(true, "Availability saved.", a));
    }
}
