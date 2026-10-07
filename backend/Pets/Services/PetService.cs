using backend.Identity.Services;
using backend.Pets.Dtos;
using backend.Pets.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Pets.Services
{
    public interface IPetService
    {
        Task<PetResponse> GetPetsAsync(int userId);
        Task<PetResponse> GetPetAsync(int userId, int petId);
        Task<PetResponse> CreatePetAsync(int userId, CreatePetRequest request);
        Task<PetResponse> UpdatePetAsync(int userId, int petId, UpdatePetRequest request);
        Task<PetResponse> DeletePetAsync(int userId, int petId);
        Task<PetResponse> UploadPictureAsync(int userId, int petId, IFormFile file);
        Task<PetResponse> DeletePictureAsync(int userId, int petId, int pictureId);
    }

    public class PetService : IPetService
    {
        private readonly AppDbContext _context;
        private readonly IBlobService _blobService;
        private readonly ILogger<PetService> _logger;

        public PetService(AppDbContext context, IBlobService blobService, ILogger<PetService> logger)
        {
            _context = context;
            _blobService = blobService;
            _logger = logger;
        }

        public async Task<PetResponse> GetPetsAsync(int userId)
        {
            var pets = await _context.Pets
                .Where(p => p.UserId == userId)
                .Include(p => p.Pictures)
                .OrderBy(p => p.CreatedAt)
                .ToListAsync();

            return new PetResponse
            {
                Success = true,
                Pets = pets.Select(MapToDto).ToList()
            };
        }

        public async Task<PetResponse> GetPetAsync(int userId, int petId)
        {
            var pet = await _context.Pets
                .Include(p => p.Pictures)
                .FirstOrDefaultAsync(p => p.Id == petId && p.UserId == userId);

            if (pet == null)
                return new PetResponse { Success = false, Message = "Pet not found." };

            return new PetResponse { Success = true, Pet = MapToDto(pet) };
        }

        public async Task<PetResponse> CreatePetAsync(int userId, CreatePetRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                    return new PetResponse { Success = false, Message = "Name is required." };

                if (!Enum.TryParse<PetGender>(request.Gender, true, out var gender))
                    return new PetResponse { Success = false, Message = "Invalid gender. Valid values: Male, Female." };

                if (!Enum.TryParse<PetType>(request.Type, true, out var petType))
                    return new PetResponse { Success = false, Message = "Invalid type. Valid values: Cat, Dog." };

                if (request.Age < 0)
                    return new PetResponse { Success = false, Message = "Age must be a non-negative number." };

                var pet = new Pet
                {
                    UserId = userId,
                    Name = request.Name.Trim(),
                    Age = request.Age,
                    Gender = gender,
                    Type = petType,
                    SpecialNeeds = request.SpecialNeeds?.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Pets.Add(pet);
                await _context.SaveChangesAsync();

                return new PetResponse { Success = true, Message = "Pet created.", Pet = MapToDto(pet) };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating pet: {ex.Message}");
                return new PetResponse { Success = false, Message = "An error occurred while creating the pet." };
            }
        }

        public async Task<PetResponse> UpdatePetAsync(int userId, int petId, UpdatePetRequest request)
        {
            try
            {
                var pet = await _context.Pets
                    .Include(p => p.Pictures)
                    .FirstOrDefaultAsync(p => p.Id == petId && p.UserId == userId);

                if (pet == null)
                    return new PetResponse { Success = false, Message = "Pet not found." };

                if (string.IsNullOrWhiteSpace(request.Name))
                    return new PetResponse { Success = false, Message = "Name is required." };

                if (!Enum.TryParse<PetGender>(request.Gender, true, out var gender))
                    return new PetResponse { Success = false, Message = "Invalid gender. Valid values: Male, Female." };

                if (!Enum.TryParse<PetType>(request.Type, true, out var petType))
                    return new PetResponse { Success = false, Message = "Invalid type. Valid values: Cat, Dog." };

                if (request.Age < 0)
                    return new PetResponse { Success = false, Message = "Age must be a non-negative number." };

                pet.Name = request.Name.Trim();
                pet.Age = request.Age;
                pet.Gender = gender;
                pet.Type = petType;
                pet.SpecialNeeds = request.SpecialNeeds?.Trim();
                pet.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return new PetResponse { Success = true, Message = "Pet updated.", Pet = MapToDto(pet) };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating pet: {ex.Message}");
                return new PetResponse { Success = false, Message = "An error occurred while updating the pet." };
            }
        }

        public async Task<PetResponse> DeletePetAsync(int userId, int petId)
        {
            try
            {
                var pet = await _context.Pets
                    .FirstOrDefaultAsync(p => p.Id == petId && p.UserId == userId);

                if (pet == null)
                    return new PetResponse { Success = false, Message = "Pet not found." };

                _context.Pets.Remove(pet);
                await _context.SaveChangesAsync();

                return new PetResponse { Success = true, Message = "Pet deleted." };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting pet: {ex.Message}");
                return new PetResponse { Success = false, Message = "An error occurred while deleting the pet." };
            }
        }

        public async Task<PetResponse> UploadPictureAsync(int userId, int petId, IFormFile file)
        {
            try
            {
                var pet = await _context.Pets
                    .Include(p => p.Pictures)
                    .FirstOrDefaultAsync(p => p.Id == petId && p.UserId == userId);

                if (pet == null)
                    return new PetResponse { Success = false, Message = "Pet not found." };

                var url = await _blobService.UploadPetPictureAsync(userId, petId, file);

                var picture = new PetPicture
                {
                    PetId = petId,
                    Url = url,
                    UploadedAt = DateTime.UtcNow
                };

                _context.PetPictures.Add(picture);
                await _context.SaveChangesAsync();

                pet.Pictures.Add(picture);

                return new PetResponse { Success = true, Message = "Picture uploaded.", Pet = MapToDto(pet) };
            }
            catch (ArgumentException ex)
            {
                return new PetResponse { Success = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error uploading pet picture: {ex.Message}");
                return new PetResponse { Success = false, Message = "An error occurred while uploading the picture." };
            }
        }

        public async Task<PetResponse> DeletePictureAsync(int userId, int petId, int pictureId)
        {
            try
            {
                var pet = await _context.Pets
                    .Include(p => p.Pictures)
                    .FirstOrDefaultAsync(p => p.Id == petId && p.UserId == userId);

                if (pet == null)
                    return new PetResponse { Success = false, Message = "Pet not found." };

                var picture = pet.Pictures.FirstOrDefault(pic => pic.Id == pictureId);
                if (picture == null)
                    return new PetResponse { Success = false, Message = "Picture not found." };

                _context.PetPictures.Remove(picture);
                await _context.SaveChangesAsync();

                pet.Pictures.Remove(picture);

                return new PetResponse { Success = true, Message = "Picture deleted.", Pet = MapToDto(pet) };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting pet picture: {ex.Message}");
                return new PetResponse { Success = false, Message = "An error occurred while deleting the picture." };
            }
        }

        private static PetDto MapToDto(Pet pet) => new()
        {
            Id = pet.Id,
            UserId = pet.UserId,
            Name = pet.Name,
            Age = pet.Age,
            Gender = pet.Gender.ToString(),
            Type = pet.Type.ToString(),
            SpecialNeeds = pet.SpecialNeeds,
            CreatedAt = pet.CreatedAt,
            UpdatedAt = pet.UpdatedAt,
            Pictures = pet.Pictures.Select(pic => new PetPictureDto
            {
                Id = pic.Id,
                Url = pic.Url,
                UploadedAt = pic.UploadedAt
            }).ToList()
        };
    }
}
