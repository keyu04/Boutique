namespace Boutique.Application.Interfaces;

public record StoredImage(string Url, string StorageKey);

public interface IImageStorage
{
    Task<StoredImage> SaveAsync(Stream content, string extension, CancellationToken ct);
    Task DeleteAsync(string storageKey, CancellationToken ct);
}