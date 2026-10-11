namespace Forest.Minimal.Account.Services.Bearer;

public sealed class BearerConfiguration
{
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required string HashKey { get; init; }
    public required int Exp { get; init; }
    public required int RefreshExp { get; init; }
}