using backend.Pets.Dtos;
using backend.Pets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Pets.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PetsController : ControllerBase
    {
        private readonly IPetService _petService;

        public PetsController(IPetService petService)
        {
            _petService = petService;
        }

        [HttpGet]
        public async Task<IActionResult> GetPets()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var result = await _petService.GetPetsAsync(userId.Value);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetPet(int id)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var result = await _petService.GetPetAsync(userId.Value, id);
            if (!result.Success) return NotFound(result);
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> CreatePet([FromBody] CreatePetRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var result = await _petService.CreatePetAsync(userId.Value, request);
            if (!result.Success) return BadRequest(result);
            return CreatedAtAction(nameof(GetPet), new { id = result.Pet!.Id }, result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePet(int id, [FromBody] UpdatePetRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var result = await _petService.UpdatePetAsync(userId.Value, id, request);
            if (!result.Success) return result.Message == "Pet not found." ? NotFound(result) : BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePet(int id)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var result = await _petService.DeletePetAsync(userId.Value, id);
            if (!result.Success) return NotFound(result);
            return Ok(result);
        }

        [HttpPost("{id}/pictures")]
        public async Task<IActionResult> UploadPicture(int id, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new PetResponse { Success = false, Message = "No file provided." });

            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var result = await _petService.UploadPictureAsync(userId.Value, id, file);
            if (!result.Success) return result.Message == "Pet not found." ? NotFound(result) : BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("{id}/pictures/{pictureId}")]
        public async Task<IActionResult> DeletePicture(int id, int pictureId)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var result = await _petService.DeletePictureAsync(userId.Value, id, pictureId);
            if (!result.Success) return NotFound(result);
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
