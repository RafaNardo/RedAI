namespace RedAI.Application;
public record StoredAsset(string StorageKey, string ContentType, long Length, string PublicUrl);
public interface IAssetStorage { Task<StoredAsset> PutAsync(Stream stream, string key, string contentType, CancellationToken ct); Task<Stream> OpenReadAsync(string key, CancellationToken ct); Task DeleteAsync(string key, CancellationToken ct); string GetPublicUrl(string key); }
