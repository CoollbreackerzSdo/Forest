using System.Globalization;

namespace Forest.Minimal.Common.Services.Validation;

public sealed class ValidationConfiguration
{
    public int PasswordLength { get; init; }
    public long ValidImgFileMBSize
    {
        get; init => field = value * 1000 * 1000;
    }
    public IReadOnlyCollection<string> FileTypes { get; init; } = [];
    public bool ValidateFile(IFormFile file) => FileTypes.Contains(Path.GetExtension(file.FileName).ToLowerInvariant());
}