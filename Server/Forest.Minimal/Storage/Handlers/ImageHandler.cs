using Amazon.S3;
using Forest.Communication.Models.Storage;
using Forest.Minimal.Common.Handlers;
using RustfsClient.Abstractions;

namespace Forest.Minimal.Storage.Handlers;

public sealed class ImageHandler(IRustfsObjectStorageClient storage) : IHandler<ImageQuery, ImageResult>
{
    public async Task<Result<ImageResult>> HandlerAsync(ImageQuery request, CancellationToken token)
    {
        try
        {
            var content = new MemoryStream();
            await storage.DownloadAsync(StorageHelper.BucketName, request.ImageId, content, token);
            return new ImageResult(content, GetContentType(request.ImageId));
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return Result.NotFound();
        }
    }
    private static string GetContentType(string key)
    {
        StorageHelper.ContentTypes.TryGetContentType(key, out var contentType);
        return contentType ?? "application/octet-stream";
    }
}

public sealed record ImageQuery(string ImageId);