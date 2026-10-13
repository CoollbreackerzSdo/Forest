namespace RustfsClient.Models;

public sealed record RustfsObjectInfo(string Key, long Size, DateTimeOffset LastModified, string ETag);