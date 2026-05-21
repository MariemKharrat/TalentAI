using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CareerApp.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CareerApp.Infrastructure.Services;

public sealed class BlobStorageService
{
    private readonly BlobContainerClient _containerClient;

    public BlobStorageService(IOptions<BlobStorageOptions> options)
    {
        var config = options.Value;
        var blobServiceClient = new BlobServiceClient(config.ConnectionString);
        _containerClient = blobServiceClient.GetBlobContainerClient(config.ContainerName);
    }

    public async Task<string> UploadCvAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var blobName = $"{Guid.NewGuid()}/{fileName}";
        var blobClient = _containerClient.GetBlobClient(blobName);

        await blobClient.UploadAsync(fileStream, new BlobHttpHeaders
        {
            ContentType = GetContentType(fileName)
        }, cancellationToken: cancellationToken);

        return blobClient.Uri.ToString();
    }

    public async Task<Stream?> DownloadCvAsync(string blobUrl, CancellationToken cancellationToken = default)
    {
        var blobClient = GetBlobClient(blobUrl);

        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            return null;
        }

        var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return response.Value.Content;
    }

    public async Task DeleteCvAsync(string blobUrl, CancellationToken cancellationToken = default)
    {
        var blobClient = GetBlobClient(blobUrl);
        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    private BlobClient GetBlobClient(string blobUrl)
    {
        var uri = new Uri(blobUrl);
        var blobPath = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        var prefix = $"{_containerClient.Name}/";
        var blobName = blobPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? blobPath[prefix.Length..]
            : blobPath;

        return _containerClient.GetBlobClient(blobName);
    }

    private static string GetContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".doc" => "application/msword",
            _ => "application/octet-stream"
        };
    }
}
