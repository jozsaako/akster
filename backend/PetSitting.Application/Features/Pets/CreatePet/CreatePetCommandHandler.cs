using MediatR;
using Microsoft.Extensions.Logging;
using PetSitting.Application.Abstractions.Repositories;
using PetSitting.Domain.Pets;

namespace PetSitting.Application.Features.Pets.CreatePet;

public class CreatePetCommandHandler : IRequestHandler<CreatePetCommand, PetResponse>
{
    private readonly IPetRepository _pets;
    private readonly IUserRepository _users;
    private readonly ILogger<CreatePetCommandHandler> _logger;

    public CreatePetCommandHandler(IPetRepository pets, IUserRepository users, ILogger<CreatePetCommandHandler> logger)
    {
        _pets = pets;
        _users = users;
        _logger = logger;
    }

    public async Task<PetResponse> Handle(CreatePetCommand request, CancellationToken cancellationToken)
    {
        if (await _users.GetByIdAsync(request.UserId, cancellationToken) == null)
        {
            return new PetResponse(false, "User not found.");
        }

        // validator guarantees these parse
        var pet = new Pet
        {
            UserId = request.UserId,
            Name = request.Name.Trim(),
            Age = request.Age,
            Gender = Enum.Parse<PetGender>(request.Gender, true),
            Type = Enum.Parse<PetType>(request.Type, true),
            SpecialNeeds = request.SpecialNeeds?.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _pets.AddAsync(pet, cancellationToken);
        _logger.LogInformation("Pet created: {Name} (ID: {Id})", pet.Name, pet.Id);

        return new PetResponse(true, "Pet created.", pet.ToDto());
    }
}
