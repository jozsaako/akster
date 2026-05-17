using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace backend.Identity.Services
{
    public interface IBlobService
    {
        Task<string> UploadAvatarAsync(int userId, IFormFile file);
    }

    public class BlobService : IBlobService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _containerName;

        private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/png", "image/gif", "image/webp"
        };

        private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

        public BlobService(IConfiguration configuration)
        {
            var connectionString = configuration["AzureBlobStorage:ConnectionString"]
                ?? throw new InvalidOperationException("AzureBlobStorage:ConnectionString is not configured.");
            _containerName = configuration["AzureBlobStorage:ContainerName"] ?? "avatars";
            _blobServiceClient = new BlobServiceClient(connectionString);
        }

        public async Task<string> UploadAvatarAsync(int userId, IFormFile file)
        {
            if (file.Length == 0)
                throw new ArgumentException("File is empty.");
            if (file.Length > MaxFileSizeBytes)
                throw new ArgumentException("File exceeds the 5 MB size limit.");
            if (!AllowedContentTypes.Contains(file.ContentType))
                throw new ArgumentException("Only JPEG, PNG, GIF, and WebP images are allowed.");

            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var blobName = $"avatars/{userId}/{Guid.NewGuid()}{extension}";
            var blobClient = containerClient.GetBlobClient(blobName);

            using var stream = file.OpenReadStream();
            await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = file.ContentType });

            return blobClient.Uri.ToString();
        }
    }
}
