namespace PetSitting.Application.Common;

public record FileUpload(Stream Content, string FileName, string ContentType, long Length);

public interface IBlobService
{
    /// <exception cref="ArgumentException">The file is empty, too large or not an allowed image type.</exception>
    Task<string> UploadAvatarAsync(int userId, FileUpload file, CancellationToken cancellationToken = default);
    Task<string> UploadPetPictureAsync(int userId, int petId, FileUpload file, CancellationToken cancellationToken = default);
}
