using Amazon.S3;
using Forest.Communication.Models.Storage;
using Forest.Minimal.Common.Handlers;
using Forest.Minimal.Storage.Helpers;
using Microsoft.AspNetCore.StaticFiles;
using RustfsClient.Abstractions;

namespace Forest.Minimal.Storage.Handlers;

public sealed class ImageHandler(IRustfsObjectStorageClient storage) : IHandler<ImageQuery, ImageResult>
{
    public async Task<Result<ImageResult>> HandlerAsync(ImageQuery request, CancellationToken token)
    {
        var content = new MemoryStream();

        try
        {
            await storage.DownloadAsync(StorageHelper.BucketName, request.ImageId, content, token);
            if (content.Length == 0) return Result.NotFound();
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return Result.NotFound();
        }

        content.Position = 0;
        return new ImageResult(content, GetContentType(request.ImageId));
    }
    private static string GetContentType(string key)
    {
        StorageHelper.ContentTypes.TryGetContentType(key, out var contentType);
        return contentType ?? "application/octet-stream";
    }
}

public sealed record ImageQuery(string ImageId);