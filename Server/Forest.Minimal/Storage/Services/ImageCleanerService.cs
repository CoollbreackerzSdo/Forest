using System.Threading.Channels;
using Forest.Minimal.Storage.Helpers;
using Microsoft.Extensions.Options;
using RustfsClient.Abstractions;

namespace Forest.Minimal.Storage.Services;

public sealed class ImageCleanerService(
    ILogger<ImageCleanerService> logger,
    IServiceScopeFactory scopes,
    IOptions<ImageCleanerConfiguration> configuration) : BackgroundService
{
    private readonly ImageCleanerConfiguration _configuration = configuration.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queue = scopes.CreateScope().ServiceProvider.GetRequiredKeyedService<Channel<string>>(StorageHelper.CleanerChannel);

        logger.LogInformation("Limpiador de imagenes activo cada {Interval}.", _configuration.Interval);

        try
        {
            await Task.WhenAll(DrainAsync(queue, stoppingToken), SweepAsync(stoppingToken));
        }
        catch (OperationCanceledException) { }
    }

    private async Task DrainAsync(Channel<string> queue, CancellationToken token)
    {
        await foreach (var key in queue.Reader.ReadAllAsync(token))
            await DeleteAsync([key], token);
    }

    private async Task SweepAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(_configuration.Interval);

        do
        {
            try
            {
                await DeleteAsync(await FindOrphansAsync(token), token);
                logger.LogInformation("Barrido de imágenes imágenes huérfanas completado.");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "No se pudo completar el barrido de imágenes huérfanas.");
            }
        } while (await timer.WaitForNextTickAsync(token));
    }

    public async Task<IReadOnlyList<string>> FindOrphansAsync(CancellationToken token)
    {
        using var scope = scopes.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ForestContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IRustfsObjectStorageClient>();

        var objects = await storage.ListObjectsAsync(StorageHelper.BucketName, cancellationToken: token);

        var referenced = context.Users
            .Select(x => new { PhotoId = x.Details!.PhotoId, BannerId = x.Details!.BannerId })
            .ToArray()
            .SelectMany(x => new[] { x.PhotoId, x.BannerId })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.Ordinal);

        return [.. objects.Select(x => x.Key).Where(x => !referenced.Contains(x))];
    }

    public async Task DeleteAsync(IReadOnlyList<string> keys, CancellationToken token)
    {
        if (keys.Count == 0) return;

        using var scope = scopes.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IRustfsObjectStorageClient>();
        var deleted = 0;

        await Parallel.ForEachAsync(keys, new ParallelOptions
        {
            MaxDegreeOfParallelism = _configuration.MaxDegreeOfParallelism,
            CancellationToken = token
        },
        async (key, inner) =>
        {
            try
            {
                await storage.DeleteAsync(key, inner);
                Interlocked.Increment(ref deleted);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "No se pudo borrar la imagen {Key}.", key);
            }
        });

        if (deleted > 0) logger.LogInformation("Se borraron {Deleted} imagenes huerfanas.", deleted);
    }
}