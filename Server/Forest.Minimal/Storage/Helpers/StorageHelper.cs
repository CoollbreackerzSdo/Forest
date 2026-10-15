using Microsoft.AspNetCore.StaticFiles;

namespace Forest.Minimal.Storage.Helpers;

public sealed class StorageHelper
{
    public const string BucketName = "forest";

    /// <summary>Clave del canal donde se encolan las imagenes que han quedado huerfanas.</summary>
    public const string CleanerChannel = "img-cleaner";

    public static readonly FileExtensionContentTypeProvider ContentTypes = new();
}