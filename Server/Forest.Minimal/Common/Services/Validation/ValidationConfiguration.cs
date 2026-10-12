namespace Forest.Minimal.Common.Services.Validation;

public sealed class ValidationConfiguration
{
    public int PasswordLength { get; init; }
    public long ValidFileMBSize { get; init; }
    public IReadOnlyCollection<string> FileTypes { get; init; } = [];
}