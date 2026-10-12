namespace RustfsClient.Models;

public sealed record RustfsObjectInfo(string Key, long Size, DateTime LastModified, string ETag);