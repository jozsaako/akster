using PetSitting.Application.Common;
using PetSitting.Domain;

namespace PetSitting.Application.Pets;

public interface IPetsManager
{
    Task<Result<List<PetDto>>> GetAllAsync(int userId, CancellationToken cancellationToken = default);
    Task<Result<PetDto>> GetAsync(int userId, int petId, CancellationToken cancellationToken = default);
    Task<Result<PetDto>> CreateAsync(int userId, PetInput input, CancellationToken cancellationToken = default);
    Task<Result<PetDto>> UpdateAsync(int userId, int petId, PetInput input, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(int userId, int petId, CancellationToken cancellationToken = default);
    Task<Result<PetDto>> UploadPictureAsync(int userId, int petId, FileUpload file, CancellationToken cancellationToken = default);
    Task<Result<PetDto>> DeletePictureAsync(int userId, int petId, int pictureId, CancellationToken cancellationToken = default);
}
