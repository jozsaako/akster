using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using PetSitting.Application.Common;

namespace PetSitting.Infrastructure.Services;

public class BlobService : IBlobService
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp"
    };

    private readonly BlobServiceClient _client;
    private readonly string _containerName;

    public BlobService(IConfiguration configuration)
    {
        var connectionString = configuration["AzureBlobStorage:ConnectionString"]
            ?? throw new InvalidOperationException("AzureBlobStorage:ConnectionString is not configured.");
        _containerName = configuration["AzureBlobStorage:ContainerName"] ?? "avatars";
        _client = new BlobServiceClient(connectionString);
    }

    public Task<string> UploadAvatarAsync(int userId, FileUpload file, CancellationToken cancellationToken = default) =>
        UploadImageAsync($"avatars/{userId}/{Guid.NewGuid()}{Extension(file)}", file, cancellationToken);

    public Task<string> UploadPetPictureAsync(int userId, int petId, FileUpload file, CancellationToken cancellationToken = default) =>
        UploadImageAsync($"pets/{userId}/{petId}/{Guid.NewGuid()}{Extension(file)}", file, cancellationToken);

    private static string Extension(FileUpload file) => Path.GetExtension(file.FileName).ToLowerInvariant();

    private async Task<string> UploadImageAsync(string blobPath, FileUpload file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
            throw new ArgumentException("File is empty.");
        if (file.Length > MaxFileSizeBytes)
            throw new ArgumentException("File exceeds the 5 MB size limit.");
        if (!AllowedContentTypes.Contains(file.ContentType))
            throw new ArgumentException("Only JPEG, PNG, GIF, and WebP images are allowed.");

        var container = _client.GetBlobContainerClient(_containerName);
        await container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);

        var blob = container.GetBlobClient(blobPath);
        await blob.UploadAsync(file.Content, new BlobHttpHeaders { ContentType = file.ContentType }, cancellationToken: cancellationToken);

        return blob.Uri.ToString();
    }
}
