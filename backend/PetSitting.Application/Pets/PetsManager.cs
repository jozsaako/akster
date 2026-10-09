using Microsoft.Extensions.Logging;
using PetSitting.Application.Common;
using PetSitting.Application.Users;
using PetSitting.Domain;
using PetSitting.Domain.Pets;

namespace PetSitting.Application.Pets;

public class PetsManager : IPetsManager
{
    private const string PetNotFound = "Pet not found.";

    private readonly IPetRepository _petsRepository;
    private readonly IUsersManager _usersManager;
    private readonly IBlobService _blobService;
    private readonly ILogger<PetsManager> _logger;

    public PetsManager(
        IPetRepository petsRepository,
        IUsersManager usersManager,
        IBlobService blobService,
        ILogger<PetsManager> logger)
    {
        _petsRepository = petsRepository;
        _usersManager = usersManager;
        _blobService = blobService;
        _logger = logger;
    }

    public async Task<Result<List<PetDto>>> GetAllAsync(int userId, CancellationToken cancellationToken = default)
    {
        var pets = await _petsRepository.GetByUserIdAsync(userId, cancellationToken);
        return Result<List<PetDto>>.Ok(pets.Select(p => p.ToDto()).ToList());
    }

    public async Task<Result<PetDto>> GetAsync(int userId, int petId, CancellationToken cancellationToken = default)
    {
        var pet = await LoadOwnedAsync(userId, petId, cancellationToken);
        return pet == null
            ? Result<PetDto>.Fail(ErrorKind.NotFound, PetNotFound)
            : Result<PetDto>.Ok(pet.ToDto());
    }

    public async Task<Result<PetDto>> CreateAsync(int userId, PetInput input, CancellationToken cancellationToken = default)
    {
        if (!await _usersManager.ExistsAsync(userId, cancellationToken))
            return Result<PetDto>.Fail(ErrorKind.NotFound, "User not found.");

        var pet = new Pet
        {
            UserId = userId,
            Name = input.Name.Trim(),
            Age = input.Age,
            Gender = input.Gender,
            Type = input.Type,
            SpecialNeeds = input.SpecialNeeds?.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _petsRepository.AddAsync(pet, cancellationToken);
        _logger.LogInformation("Pet created: {Name} (ID: {Id})", pet.Name, pet.Id);

        return Result<PetDto>.Ok(pet.ToDto());
    }

    public async Task<Result<PetDto>> UpdateAsync(int userId, int petId, PetInput input, CancellationToken cancellationToken = default)
    {
        var pet = await LoadOwnedAsync(userId, petId, cancellationToken);
        if (pet == null) return Result<PetDto>.Fail(ErrorKind.NotFound, PetNotFound);

        pet.Name = input.Name.Trim();
        pet.Age = input.Age;
        pet.Gender = input.Gender;
        pet.Type = input.Type;
        pet.SpecialNeeds = input.SpecialNeeds?.Trim();
        pet.UpdatedAt = DateTime.UtcNow;

        await _petsRepository.UpdateAsync(pet, cancellationToken);
        return Result<PetDto>.Ok(pet.ToDto());
    }

    public async Task<Result> DeleteAsync(int userId, int petId, CancellationToken cancellationToken = default)
    {
        var pet = await LoadOwnedAsync(userId, petId, cancellationToken);
        if (pet == null) return Result.Fail(ErrorKind.NotFound, PetNotFound);

        await _petsRepository.DeleteAsync(pet.Id, cancellationToken);
        return Result.Ok();
    }

    public async Task<Result<PetDto>> UploadPictureAsync(int userId, int petId, FileUpload file, CancellationToken cancellationToken = default)
    {
        var pet = await LoadOwnedAsync(userId, petId, cancellationToken);
        if (pet == null) return Result<PetDto>.Fail(ErrorKind.NotFound, PetNotFound);

        string url;
        try
        {
            url = await _blobService.UploadPetPictureAsync(userId, pet.Id, file, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return Result<PetDto>.Fail(ErrorKind.Validation, ex.Message);
        }

        pet.Pictures.Add(new PetPicture { Url = url, UploadedAt = DateTime.UtcNow });
        await _petsRepository.UpdateAsync(pet, cancellationToken);

        return Result<PetDto>.Ok(pet.ToDto());
    }

    public async Task<Result<PetDto>> DeletePictureAsync(int userId, int petId, int pictureId, CancellationToken cancellationToken = default)
    {
        var pet = await LoadOwnedAsync(userId, petId, cancellationToken);
        if (pet == null) return Result<PetDto>.Fail(ErrorKind.NotFound, PetNotFound);

        var picture = pet.Pictures.FirstOrDefault(p => p.Id == pictureId);
        if (picture == null) return Result<PetDto>.Fail(ErrorKind.NotFound, "Picture not found.");

        pet.Pictures.Remove(picture);
        await _petsRepository.UpdateAsync(pet, cancellationToken);

        return Result<PetDto>.Ok(pet.ToDto());
    }

    // Missing and foreign pets look the same so ids cannot be probed.
    private async Task<Pet?> LoadOwnedAsync(int userId, int petId, CancellationToken cancellationToken)
    {
        var pet = await _petsRepository.GetByIdAsync(petId, cancellationToken);
        return pet == null || pet.UserId != userId ? null : pet;
    }
}
