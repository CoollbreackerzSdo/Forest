using Amazon.S3;
using RustfsClient.Abstractions;
using RustfsClient.Models;

namespace Forest.Test.Support;

/// <summary>
/// Doble en memoria de <see cref="IRustfsObjectStorageClient"/>.
/// Reproduce el comportamiento relevante de RustFS/S3: bucket explicito y
/// <see cref="AmazonS3Exception"/> con <see cref="HttpStatusCode.NotFound"/> al descargar una clave inexistente.
/// </summary>
public sealed class FakeRustfsObjectStorageClient : IRustfsObjectStorageClient
{
    private readonly Dictionary<string, Dictionary<string, FakeObject>> _buckets = new(StringComparer.Ordinal);
    private readonly HashSet<string> _failing = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    public List<string> EnsuredBuckets { get; } = [];
    public List<string> DeletedKeys { get; } = [];

    /// <summary>Mayor numero de borrados observados a la vez, para verificar el paralelismo.</summary>
    public int MaxObservedConcurrency { get; private set; }

    /// <summary>Hace que <see cref="DeleteAsync"/> falle para esa clave.</summary>
    public void FailOn(string objectKey) => _failing.Add(objectKey);

    public IReadOnlyDictionary<string, byte[]> Objects(string bucketName)
    {
        lock (_gate)
            return _buckets.TryGetValue(bucketName, out var bucket)
                ? bucket.ToDictionary(x => x.Key, x => x.Value.Content, StringComparer.Ordinal)
                : new Dictionary<string, byte[]>(StringComparer.Ordinal);
    }

    public Task UploadAsync(string bucketName, string objectKey, Stream content, string? contentType = null, long? contentLength = null, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        lock (_gate) Bucket(bucketName)[objectKey] = new(buffer.ToArray(), contentType);
        return Task.CompletedTask;
    }

    public Task EnsureBucketAsync(string bucketName, CancellationToken cancellationToken = default)
    {
        EnsuredBuckets.Add(bucketName);
        lock (_gate) Bucket(bucketName);
        return Task.CompletedTask;
    }

    public Task DownloadAsync(string bucketName, string objectKey, Stream destination, CancellationToken cancellationToken = default)
    {
        byte[]? content;
        lock (_gate) content = _buckets.TryGetValue(bucketName, out var bucket) && bucket.TryGetValue(objectKey, out var value) ? value.Content : null;

        return content is null ? throw NotFound(bucketName, objectKey) : destination.WriteAsync(content, cancellationToken).AsTask();
    }

    public Task<IReadOnlyList<RustfsObjectInfo>> ListObjectsAsync(string bucketName, string? prefix = null, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<RustfsObjectInfo> objects;
        lock (_gate) objects = _buckets.TryGetValue(bucketName, out var bucket)
            ? bucket
                .Where(x => prefix is null || x.Key.StartsWith(prefix, StringComparison.Ordinal))
                .Select(x => new RustfsObjectInfo(x.Key, x.Value.Content.LongLength, DateTimeOffset.UnixEpoch, x.Value.Content.LongLength.ToString()))
                .ToList()
            : [];
        return Task.FromResult(objects);
    }

    public async Task DeleteAsync(string bucketName, string objectKey, CancellationToken cancellationToken = default)
    {
        int inFlight;
        lock (_gate)
        {
            inFlight = ++_inFlight;
            MaxObservedConcurrency = Math.Max(MaxObservedConcurrency, inFlight);
        }

        // Una pausa minima hace observable la concurrencia real de Parallel.ForEachAsync.
        await Task.Delay(20, cancellationToken);

        lock (_gate)
        {
            _inFlight--;
            if (_failing.Contains(objectKey)) throw new InvalidOperationException($"Fallo simulado en {objectKey}.");
            DeletedKeys.Add(objectKey);
            // S3 DELETE es idempotente: borrar una clave que no existe no es error.
            if (_buckets.TryGetValue(bucketName, out var bucket)) bucket.Remove(objectKey);
        }
    }

    public void Dispose()
    {
        lock (_gate) _buckets.Clear();
    }

    private Dictionary<string, FakeObject> Bucket(string bucketName)
    {
        if (!_buckets.TryGetValue(bucketName, out var bucket))
            _buckets[bucketName] = bucket = new(StringComparer.Ordinal);
        return bucket;
    }

    private static AmazonS3Exception NotFound(string bucketName, string objectKey)
    {
        var exception = new AmazonS3Exception($"The specified key does not exist. (bucket: {bucketName}, key: {objectKey})");
        exception.StatusCode = HttpStatusCode.NotFound;
        return exception;
    }

    private int _inFlight;
    private sealed record FakeObject(byte[] Content, string? ContentType);
}