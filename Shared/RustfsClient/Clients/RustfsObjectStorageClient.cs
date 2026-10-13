using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using RustfsClient.Abstractions;
using RustfsClient.Models;
using RustfsClient.Options;

namespace RustfsClient.Clients;

public sealed class RustfsObjectStorageClient : IRustfsObjectStorageClient
{
    public RustfsObjectStorageClient(RustfsClientOptions options)
        : this(CreateS3Client(options), ownsClient: true)
    {
    }

    public RustfsObjectStorageClient(IAmazonS3 s3Client)
        : this(s3Client, ownsClient: false)
    {
    }

    private RustfsObjectStorageClient(IAmazonS3 s3Client, bool ownsClient)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _ownsClient = ownsClient;
    }

    public async Task DownloadAsync(string bucketName, string objectKey, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        using var response = await _s3Client.GetObjectAsync(
            new GetObjectRequest { BucketName = bucketName, Key = objectKey },
            cancellationToken);

        await response.ResponseStream.CopyToAsync(destination, cancellationToken);
    }

    public async Task<IReadOnlyList<RustfsObjectInfo>> ListObjectsAsync(string bucketName, string? prefix = null, CancellationToken cancellationToken = default)
    {
        var objects = new List<RustfsObjectInfo>();
        string? continuationToken = null;

        do
        {
            var response = await _s3Client.ListObjectsV2Async(
                new ListObjectsV2Request
                {
                    BucketName = bucketName,
                    Prefix = prefix,
                    ContinuationToken = continuationToken
                },
                cancellationToken);

            objects.AddRange(response.S3Objects.Select(item => new RustfsObjectInfo(
                item.Key,
                item.Size ?? 0,
                item.LastModified ?? default,
                item.ETag)));

            continuationToken = response.IsTruncated ?? false ? response.NextContinuationToken : null;
        }
        while (continuationToken is not null);
        
        return objects;
    }

    public Task DeleteAsync(string bucketName, string objectKey, CancellationToken cancellationToken = default)
    {
        return _s3Client.DeleteObjectAsync(
            new DeleteObjectRequest { BucketName = bucketName, Key = objectKey },
            cancellationToken);
    }

    public async Task EnsureBucketAsync(string bucketName, CancellationToken cancellationToken = default)
    {
        try
        {
            await _s3Client.PutBucketAsync(new PutBucketRequest { BucketName = bucketName }, cancellationToken);
        }
        catch (AmazonS3Exception exception) when (exception.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists"
            || exception.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            // El bucket ya existe: la operación se considera exitosa.
        }
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _s3Client.Dispose();
        }
    }

    private static IAmazonS3 CreateS3Client(RustfsClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!Uri.TryCreate(options.ServiceUrl, UriKind.Absolute, out var serviceUri) ||
            (serviceUri.Scheme != Uri.UriSchemeHttp && serviceUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("ServiceUrl debe ser una URL absoluta HTTP o HTTPS.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.AccessKey))
        {
            throw new ArgumentException("AccessKey es obligatorio.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.SecretKey))
        {
            throw new ArgumentException("SecretKey es obligatorio.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.Region))
        {
            throw new ArgumentException("Region es obligatorio.", nameof(options));
        }

        var configuration = new AmazonS3Config
        {
            ServiceURL = serviceUri.AbsoluteUri,
            ForcePathStyle = true,
            AuthenticationRegion = options.Region
        };

        return new AmazonS3Client(
            new BasicAWSCredentials(options.AccessKey, options.SecretKey),
            configuration);
    }

    public async Task UploadAsync(string bucketName, string objectKey, Stream content, string? contentType = null, long? contentLength = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);

        var request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            InputStream = content,
            AutoCloseStream = false,
            ContentType = contentType ?? "application/octet-stream"
        };

        if (contentLength.HasValue)
            request.Headers.ContentLength = contentLength.Value;
        else if (content.CanSeek)
            request.Headers.ContentLength = content.Length;

        await _s3Client.PutObjectAsync(request, cancellationToken);
    }

    private readonly IAmazonS3 _s3Client;
    private readonly bool _ownsClient;
}