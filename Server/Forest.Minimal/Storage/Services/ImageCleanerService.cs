using System.Threading.Channels;
using Forest.Minimal.Storage.Helpers;
using Microsoft.Extensions.Options;
using RustfsClient.Abstractions;

namespace Forest.Minimal.Storage.Services;

/// <summary>
/// Borra en segundo plano las imagenes que ya no referencia ningun usuario.<para>
/// Dos caminos alimentan el borrado:</para>
/// <list type="number">
/// <item>El canal, que el handler escribe con <c>TryWrite</c> cuando una imagen deja de
/// estar referenciada. No espera: la peticion responde en cuanto se encola.</item>
/// <item>Un barrido periodico que compara el bucket contra la base de datos. Es la red de
/// seguridad: limpia lo que se haya quedado en el canal, lo que se perdio al encolar y lo
/// que dejo huérfano un proceso anterior al caerse.</item>
/// </list>
/// </summary>
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
        catch (OperationCanceledException)
        {
            // Apagado ordenado del host.
        }
    }

    /// <summary>Consume el canal sin bloquear a nadie: el escritor usa TryWrite.</summary>
    private async Task DrainAsync(Channel<string> queue, CancellationToken token)
    {
        await foreach (var key in queue.Reader.ReadAllAsync(token))
            await DeleteAsync([key], token);
    }

    /// <summary>Barrido periodico de seguridad sobre todo el bucket.</summary>
    private async Task SweepAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(_configuration.Interval);

        while (await timer.WaitForNextTickAsync(token))
        {
            try
            {
                await DeleteAsync(await FindOrphansAsync(token), token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "No se pudo completar el barrido de imagenes huerfanas.");
            }
        }
    }

    /// <summary>Claves del bucket que ningun usuario referencia en Details.</summary>
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
            .Where(x => string.IsNullOrWhiteSpace(x));

        return [.. objects.Select(x => x.Key).Where(x => !referenced.Contains(x))];
    }

    /// <summary>Borra las claves en paralelo. Un fallo puntual no corta el resto.</summary>
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