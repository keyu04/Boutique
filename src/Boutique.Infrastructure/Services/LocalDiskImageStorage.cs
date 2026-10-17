using Boutique.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Boutique.Infrastructure.Services;

public class LocalDiskImageStorage : IImageStorage
{
    private readonly string _folder;
    private readonly string _baseUrl;

    public LocalDiskImageStorage(IConfiguration config)
    {
        _folder = Path.GetFullPath(config["ImageStorage:LocalFolder"] ?? "uploads");
        _baseUrl = (config["ImageStorage:PublicBaseUrl"] ?? "https://localhost:7000/uploads").TrimEnd('/');
        Directory.CreateDirectory(_folder);
    }

    public async Task<StoredImage> SaveAsync(Stream content, string extension, CancellationToken ct)
    {
        var key = $"{Guid.NewGuid():N}{extension}";          // we choose the name, never the user
        var path = Path.Combine(_folder, key);
        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);
        return new StoredImage($"{_baseUrl}/{key}", key);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        var path = Path.Combine(_folder, Path.GetFileName(storageKey)); // GetFileName blocks "../" tricks
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
    public string GetUrl(string storageKey) => $"{_baseUrl}/{storageKey}";
}