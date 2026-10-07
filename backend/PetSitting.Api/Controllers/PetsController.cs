using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetSitting.Api.Contracts;
using PetSitting.Application.Features.Pets.CreatePet;
using PetSitting.Application.Features.Pets.DeletePet;
using PetSitting.Application.Features.Pets.DeletePetPicture;
using PetSitting.Application.Features.Pets.GetPet;
using PetSitting.Application.Features.Pets.GetPets;
using PetSitting.Application.Features.Pets.UpdatePet;
using PetSitting.Application.Features.Pets.UploadPetPicture;

namespace PetSitting.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PetsController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public PetsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetPets() =>
        Ok(await _mediator.Send(new GetPetsQuery(UserId)));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetPet(int id)
    {
        var result = await _mediator.Send(new GetPetQuery(UserId, id));
        return Respond(result.Success, result, result.Message);
    }

    [HttpPost]
    public async Task<IActionResult> CreatePet([FromBody] PetRequest r)
    {
        var result = await _mediator.Send(new CreatePetCommand(UserId, r.Name, r.Age, r.Gender, r.Type, r.SpecialNeeds));
        return result.Success ? CreatedAtAction(nameof(GetPet), new { id = result.Pet!.Id }, result) : BadRequest(result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePet(int id, [FromBody] PetRequest r)
    {
        var result = await _mediator.Send(new UpdatePetCommand(UserId, id, r.Name, r.Age, r.Gender, r.Type, r.SpecialNeeds));
        return Respond(result.Success, result, result.Message);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePet(int id)
    {
        var result = await _mediator.Send(new DeletePetCommand(UserId, id));
        return Respond(result.Success, result, result.Message);
    }

    [HttpPost("{id}/pictures")]
    public async Task<IActionResult> UploadPicture(int id, IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { Success = false, Message = "No file provided." });

        var result = await _mediator.Send(new UploadPetPictureCommand(UserId, id, ToUpload(file)));
        return Respond(result.Success, result, result.Message);
    }

    [HttpDelete("{id}/pictures/{pictureId}")]
    public async Task<IActionResult> DeletePicture(int id, int pictureId)
    {
        var result = await _mediator.Send(new DeletePetPictureCommand(UserId, id, pictureId));
        return Respond(result.Success, result, result.Message);
    }
}
