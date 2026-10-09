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

    [HttpPut]
    public async Task<IActionResult> UpsertAvailability([FromBody] AvailabilityRequest r)
    {
        var result = await _availabilityManager.UpsertAsync(
            UserId, new UpsertAvailabilityInput(r.Schedule, r.Services, r.AcceptedPetTypes, r.MaxPets, r.Bio));
        return ToAction(result, a => new AvailabilityResponse(true, "Availability saved.", a));
    }
}
