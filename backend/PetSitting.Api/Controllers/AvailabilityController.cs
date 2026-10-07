using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetSitting.Api.Contracts;
using PetSitting.Application.Features.Availability.GetAvailability;
using PetSitting.Application.Features.Availability.UpsertAvailability;

namespace PetSitting.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AvailabilityController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public AvailabilityController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAvailability() =>
        Ok(await _mediator.Send(new GetAvailabilityQuery(UserId)));

    [HttpPut]
    public async Task<IActionResult> UpsertAvailability([FromBody] AvailabilityRequest r)
    {
        var result = await _mediator.Send(
            new UpsertAvailabilityCommand(UserId, r.Schedule, r.Services, r.AcceptedPetTypes, r.MaxPets, r.Bio));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
