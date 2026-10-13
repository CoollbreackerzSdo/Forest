using RustfsClient.Models;

namespace RustfsClient.Abstractions;

public interface IRustfsObjectStorageClient : IDisposable
{
    Task UploadAsync(string bucketName, string objectKey, Stream content, string? contentType = null, long? contentLength = null, CancellationToken cancellationToken = default);
    Task EnsureBucketAsync(string bucketName, CancellationToken cancellationToken = default);
    Task DownloadAsync(string bucketName, string objectKey, Stream destination, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RustfsObjectInfo>> ListObjectsAsync(string bucketName, string? prefix = null, CancellationToken cancellationToken = default);
    Task DeleteAsync(string bucketName, string objectKey, CancellationToken cancellationToken = default);
}