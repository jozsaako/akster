using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetSitting.Api.Contracts;
using PetSitting.Application.Pets;

namespace PetSitting.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PetsController : ApiControllerBase
{
    private readonly IPetsManager _petsManager;

    public PetsController(IPetsManager petsManager)
    {
        _petsManager = petsManager;
    }

    [HttpGet]
    public async Task<IActionResult> GetPets() =>
        ToAction(await _petsManager.GetAllAsync(UserId), pets => new PetResponse(true, string.Empty, Pets: pets));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetPet(int id) =>
        ToAction(await _petsManager.GetAsync(UserId, id), pet => new PetResponse(true, string.Empty, pet));

    [HttpPost]
    public async Task<IActionResult> CreatePet([FromBody] PetRequest r)
    {
        var result = await _petsManager.CreateAsync(UserId, ToInput(r));
        return result.Match<IActionResult>(
            pet => CreatedAtAction(nameof(GetPet), new { id = pet.Id }, new PetResponse(true, "Pet created.", pet)),
            Fail);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePet(int id, [FromBody] PetRequest r) =>
        ToAction(await _petsManager.UpdateAsync(UserId, id, ToInput(r)), pet => new PetResponse(true, "Pet updated.", pet));

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePet(int id) =>
        ToAction(await _petsManager.DeleteAsync(UserId, id), new PetResponse(true, "Pet deleted."));

    [HttpPost("{id}/pictures")]
    public async Task<IActionResult> UploadPicture(int id, IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { Success = false, Message = "No file provided." });

        return ToAction(await _petsManager.UploadPictureAsync(UserId, id, ToUpload(file)),
            pet => new PetResponse(true, "Picture uploaded.", pet));
    }

    [HttpDelete("{id}/pictures/{pictureId}")]
    public async Task<IActionResult> DeletePicture(int id, int pictureId) =>
        ToAction(await _petsManager.DeletePictureAsync(UserId, id, pictureId), pet => new PetResponse(true, "Picture deleted.", pet));

    private static PetInput ToInput(PetRequest r) => new(r.Name, r.Age, r.Gender!.Value, r.Type!.Value, r.SpecialNeeds);
}
