namespace Forest.Minimal.Storage.Services;

/// <summary>
/// Ajustes del <see cref="ImageCleanerService"/>.
/// </summary>
public sealed class ImageCleanerConfiguration
{
    /// <summary>Cada cuanto tiempo se barren las imagenes huerfanas del bucket.</summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Cantidad de borrados simultaneos contra el almacenamiento.</summary>
    public int MaxDegreeOfParallelism { get; init; } = 8;
}