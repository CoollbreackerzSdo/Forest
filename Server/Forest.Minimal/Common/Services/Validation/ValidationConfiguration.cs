namespace Forest.Minimal.Common.Services.Validation;

public sealed class ValidationConfiguration
{
    public int PasswordLength { get; init; }
    public long ValidFileMBSize
    {
        get; init => field = value * 1000 * 1000;
    }
    public IReadOnlyCollection<string> FileTypes { get; init; } = [];
}