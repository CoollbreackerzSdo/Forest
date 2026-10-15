using System.Threading.Channels;
using Forest.Minimal.Account.Models;
using Forest.Minimal.Storage.Services;
using Forest.Test.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RustfsClient.Abstractions;

namespace Forest.Test.Unit.Storage;

public sealed class ImageCleanerServiceTests : IDisposable
{
    private readonly ForestContextScope _scope = ForestContextScope.Create();
    private readonly FakeRustfsObjectStorageClient _storage = new();
    private readonly Channel<string> _queue = Channel.CreateUnbounded<string>();
    private readonly ServiceProvider _provider;
    private readonly ImageCleanerService _cleaner;

    public ImageCleanerServiceTests()
    {
        _provider = new ServiceCollection()
            .AddSingleton(_scope.Context)
            .AddSingleton<IRustfsObjectStorageClient>(_storage)
            .AddKeyedSingleton<Channel<string>>(StorageHelper.CleanerChannel, (_, _) => _queue)
            .BuildServiceProvider();

        _cleaner = Build(TimeSpan.FromMinutes(5));
    }

    private ImageCleanerService Build(TimeSpan interval, int parallelism = 8)
        => new(NullLogger<ImageCleanerService>.Instance,
            new TestServiceScopeFactory(_provider),
            Options.Create(new ImageCleanerConfiguration { Interval = interval, MaxDegreeOfParallelism = parallelism }));

    private Task Upload(string key) => _storage.UploadAsync(StorageHelper.BucketName, key, new MemoryStream([1]));

    private int _sequence;

    private void Reference(params (EntityId User, string? Photo, string? Banner)[] users)
    {
        foreach (var user in users)
            _scope.Context.Users.Add(new UserModel
            {
                // Los GUID v7 comparten prefijo de timestamp: se toma el final, que si es unico.
                UserName = $"{Guid.CreateVersion7():N}"[..8] + $"{++_sequence:D2}",
                Security = new Security($"{Guid.CreateVersion7()}@forest.dev", "hash"),
                Details = new Details { PhotoId = user.Photo, BannerId = user.Banner }
            });
        _scope.Context.SaveChanges();
    }

    #region FindOrphansAsync

    [Fact]
    public async Task FindOrphans_DebeDevolverLoQueNoReferenciaNingunUsuario()
    {
        await Upload("huerfana-1.png");
        await Upload("huerfana-2.png");

        var orphans = await _cleaner.FindOrphansAsync(CancellationToken.None);

        Assert.Equal(["huerfana-1.png", "huerfana-2.png"], orphans.OrderBy(x => x));
    }

    [Fact]
    public async Task FindOrphans_NoDebeDevolverLasFotosNiLasPortadasEnUso()
    {
        await Upload("foto.png");
        await Upload("portada.png");
        await Upload("suelta.png");
        Reference((Guid.CreateVersion7(), "foto.png", "portada.png"));

        var orphans = await _cleaner.FindOrphansAsync(CancellationToken.None);

        Assert.Equal(["suelta.png"], orphans);
    }

    [Fact]
    public async Task FindOrphans_NoDebeDevolverNadaSiElBucketEstaVacio()
    {
        Assert.Empty(await _cleaner.FindOrphansAsync(CancellationToken.None));
    }

    [Fact]
    public async Task FindOrphans_NoDebeDevolverNadaSiTodosLosUsuariosTienenImagenes()
    {
        await Upload("a.png");
        await Upload("b.png");
        await Upload("c.png");
        Reference((Guid.CreateVersion7(), "a.png", "b.png"), (Guid.CreateVersion7(), "c.png", null));

        Assert.Empty(await _cleaner.FindOrphansAsync(CancellationToken.None));
    }

    [Fact]
    public async Task FindOrphans_DebeContarLasImagenesDeVariosUsuarios()
    {
        await Upload("compartida.png");
        await Upload("huerfana.png");
        Reference((Guid.CreateVersion7(), "compartida.png", null), (Guid.CreateVersion7(), "compartida.png", null));

        Assert.Equal(["huerfana.png"], await _cleaner.FindOrphansAsync(CancellationToken.None));
    }

    #endregion

    #region DeleteAsync

    [Fact]
    public async Task Delete_DeberiaBorrarTodasLasClavesRecibidas()
    {
        await Upload("a.png");
        await Upload("b.png");

        await _cleaner.DeleteAsync(["a.png", "b.png"], CancellationToken.None);

        Assert.Empty(_storage.Objects(StorageHelper.BucketName));
        Assert.Equal(["a.png", "b.png"], _storage.DeletedKeys.Order());
    }

    [Fact]
    public async Task Delete_NoDeberiaHacerNadaSiNoRecibeClaves()
    {
        await Upload("a.png");

        await _cleaner.DeleteAsync([], CancellationToken.None);

        Assert.Single(_storage.Objects(StorageHelper.BucketName));
        Assert.Empty(_storage.DeletedKeys);
    }

    [Fact]
    public async Task Delete_ShouldContinueWhenOneKeyFails()
    {
        await Upload("a.png");
        await Upload("b.png");
        await Upload("c.png");
        _storage.FailOn("b.png");

        await _cleaner.DeleteAsync(["a.png", "b.png", "c.png"], CancellationToken.None);

        // El fallo de "b.png" no impide borrar las demas.
        Assert.Equal(["b.png"], _storage.Objects(StorageHelper.BucketName).Keys);
        Assert.Equal(["a.png", "c.png"], _storage.DeletedKeys.Order());
    }

    [Fact]
    public async Task Delete_ShouldHonourMaxDegreeOfParallelism()
    {
        for (var i = 0; i < 20; i++) await Upload($"k{i}.png");
        var cleaner = Build(TimeSpan.FromMinutes(5), parallelism: 4);

        await cleaner.DeleteAsync([.. Enumerable.Range(0, 20).Select(i => $"k{i}.png")], CancellationToken.None);

        Assert.Equal(20, _storage.DeletedKeys.Count);
        Assert.InRange(_storage.MaxObservedConcurrency, 1, 4);
    }

    #endregion

    #region ExecuteAsync

    [Fact]
    public async Task Execute_DebeConsumirElCanalYTirarLoEncolado()
    {
        await Upload("encolada.png");
        var cleaner = Build(TimeSpan.FromHours(1));

        await cleaner.StartAsync(CancellationToken.None);
        await _queue.Writer.WriteAsync("encolada.png");
        await WaitUntilAsync(() => _storage.DeletedKeys.Count == 1);
        await cleaner.StopAsync(CancellationToken.None);

        Assert.Empty(_storage.Objects(StorageHelper.BucketName));
    }

    [Fact]
    public async Task Execute_DebeBarrerElBucketEnCadaIntervalo()
    {
        await Upload("huerfana.png");
        await Upload("en-uso.png");
        Reference((Guid.CreateVersion7(), "en-uso.png", null));
        var cleaner = Build(TimeSpan.FromMilliseconds(100));

        await cleaner.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => _storage.Objects(StorageHelper.BucketName).Count == 1);
        await cleaner.StopAsync(CancellationToken.None);

        Assert.Equal(["en-uso.png"], _storage.Objects(StorageHelper.BucketName).Keys);
        Assert.Equal(["huerfana.png"], _storage.DeletedKeys);
    }

    [Fact]
    public async Task Execute_NoDeberiaArrancarBloqueado()
    {
        var cleaner = Build(TimeSpan.FromHours(1));

        // StartAsync debe completarse aunque no haya nada que leer ni el primer tick haya ocurrido.
        await cleaner.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        await cleaner.StopAsync(CancellationToken.None);
    }

    #endregion

    private static async Task WaitUntilAsync(Func<bool> condition, int timeout = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeout);
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(25);
        }
        throw new TimeoutException("No se cumplio la condicion esperada.");
    }

    public void Dispose()
    {
        _provider.Dispose();
        _scope.Dispose();
        _storage.Dispose();
        GC.SuppressFinalize(this);
    }
}