using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Boutique.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Boutique.Infrastructure.Services;

public class S3ImageStorage : IImageStorage
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;
    private readonly string _baseUrl;

    public S3ImageStorage(IConfiguration config)
    {
        _bucket = config["ImageStorage:S3:Bucket"] ?? throw new InvalidOperationException("ImageStorage:S3:Bucket missing");
        _baseUrl = (config["ImageStorage:PublicBaseUrl"] ?? throw new InvalidOperationException("ImageStorage:PublicBaseUrl missing")).TrimEnd('/');

        var credentials = new BasicAWSCredentials(
            config["ImageStorage:S3:AccessKeyId"], config["ImageStorage:S3:SecretAccessKey"]);

        _s3 = new AmazonS3Client(credentials, new AmazonS3Config
        {
            ServiceURL = config["ImageStorage:S3:ServiceUrl"],        // R2 / Supabase endpoint
            AuthenticationRegion = config["ImageStorage:S3:Region"] ?? "auto",
            ForcePathStyle = true,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        });
    }

    public async Task<StoredImage> SaveAsync(Stream content, string extension, CancellationToken ct)
    {
        var key = $"{Guid.NewGuid():N}{extension}";                  // we choose the name, never the user

        // copy to memory: images are small, and a seekable stream avoids chunked-signing issues
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = buffer,
            ContentType = ContentTypeFor(extension),
            DisablePayloadSigning = true,                            // fine over HTTPS
            Headers = { CacheControl = "public, max-age=31536000, immutable" }  // names are GUIDs, never reused
        }, ct);

        return new StoredImage($"{_baseUrl}/{key}", key);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct)
        => _s3.DeleteObjectAsync(_bucket, Path.GetFileName(storageKey), ct);

    public string GetUrl(string storageKey) => $"{_baseUrl}/{storageKey}";

    private static string ContentTypeFor(string ext) => ext.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        _ => "application/octet-stream"
    };
}