using backend.Availability.Dtos;
using backend.Availability.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Availability.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AvailabilityController : ControllerBase
    {
        private readonly IAvailabilityService _availabilityService;

        public AvailabilityController(IAvailabilityService availabilityService)
        {
            _availabilityService = availabilityService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailability()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var result = await _availabilityService.GetAvailabilityAsync(userId.Value);
            return Ok(result);
        }

        [HttpPut]
        public async Task<IActionResult> UpsertAvailability([FromBody] UpdateAvailabilityRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var result = await _availabilityService.UpsertAvailabilityAsync(userId.Value, request);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        private int? GetUserId()
        {
            var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (claim == null || !int.TryParse(claim.Value, out var userId)) return null;
            return userId;
        }
    }
}
