using System.Globalization;

namespace Forest.Minimal.Common.Services.Validation;

public sealed class ValidationConfiguration
{
    public int PasswordLength { get; init; }
    public long ValidImgMBSize
    {
        get; init => field = value * 1000 * 1000;
    }
    public long ValidVideoMBSize
    {
        get; init => field = value * 1000 * 1000;
    }
    public IReadOnlyCollection<string> ValidVideoFormat { get; init; } = [];
    public IReadOnlyCollection<string> ValidImgFormat { get; init; } = [];
    public bool IsValidVideo(IFormFile file) => ValidVideoFormat.Contains(Path.GetExtension(file.FileName).ToLowerInvariant());
    public bool IsValidImg(IFormFile file) => ValidImgFormat.Contains(Path.GetExtension(file.FileName).ToLowerInvariant());
}