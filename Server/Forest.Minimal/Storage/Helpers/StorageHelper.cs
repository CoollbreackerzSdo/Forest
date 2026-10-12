using Microsoft.AspNetCore.StaticFiles;

namespace Forest.Minimal.Storage.Helpers;

public sealed class StorageHelper
{
    public const string BucketName = "forest";
    public static readonly FileExtensionContentTypeProvider ContentTypes = new();
}