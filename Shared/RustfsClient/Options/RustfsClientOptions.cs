namespace RustfsClient.Options;

public sealed class RustfsClientOptions
{
    public string ServiceUrl { get; set; } = "http://localhost:9000";
    public required string AccessKey { get; set; }
    public required string SecretKey { get; set; }
    public string Region { get; set; } = "us-east-1";
}