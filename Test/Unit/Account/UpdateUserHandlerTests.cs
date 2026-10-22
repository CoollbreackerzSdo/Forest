using System.Threading.Channels;
using Forest.Minimal.Account.Handlers;
using Forest.Minimal.Account.Models;
using Forest.Test.Support;
using Microsoft.AspNetCore.Identity;

namespace Forest.Test.Unit.Account;

public sealed class UpdateUserHandlerTests : IDisposable
{
    private readonly ForestContextScope _scope = ForestContextScope.Create();
    private readonly FakeRustfsObjectStorageClient _storage = new();
    private readonly Channel<string> _cleaner = Channel.CreateUnbounded<string>();
    private readonly UpdateUserHandler _handler;

    public UpdateUserHandlerTests()
    {
        TestData.SeedUser(_scope.Context, "usuario", "usuario@forest.dev");
        _handler = new UpdateUserHandler(_scope.Context, _storage, _cleaner);
    }

    private async Task<string[]> EnqueuedAsync()
    {
        var keys = new List<string>();
        while (_cleaner.Reader.TryRead(out var key)) keys.Add(key);
        return [.. keys];
    }

    private async Task<UserModel> ReloadAsync()
    {
        var id = await IdAsync();
        return await _scope.Context.Users.AsNoTracking().SingleAsync(x => x.Key == id);
    }

    private async Task<EntityId> IdAsync() => (await _scope.Context.Users.AsNoTracking().SingleAsync()).Key;

    private async Task<Result<Profile>> SendToUnknownUserAsync(UpdateUserRequest request)
        => await _handler.HandlerAsync(new UpdateUserQuery(Guid.CreateVersion7(), request), CancellationToken.None);

    private async Task<Result<Profile>> SendToActualUserAsync(UpdateUserRequest request)
        => await _handler.HandlerAsync(new UpdateUserQuery(await IdAsync(), request), CancellationToken.None);

    [Fact]
    public async Task HandlerAsync_DeberiaDevolverNoContentSiElUsuarioNoExiste()
    {
        var result = await SendToUnknownUserAsync(new UpdateUserRequest { UserName = "cualquiera" });

        Assert.Equal(ResultStatus.NoContent, result.Status);
    }

    [Fact]
    public async Task HandlerAsync_DeberiaIgnorarLosCamposVaciosOSoloEspacios()
    {
        var result = await SendToActualUserAsync(new UpdateUserRequest { UserName = "  ", FirstName = " ", LastName = "  " });

        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.Equal("usuario", (await ReloadAsync()).UserName);
    }

    [Fact]
    public async Task HandlerAsync_NoDebeSubirArchivosSiNoSeProporcionan()
    {
        await SendToActualUserAsync(new UpdateUserRequest());

        Assert.Empty(_storage.Objects(StorageHelper.BucketName));
        var user = await ReloadAsync();
        Assert.Null(user.Details?.PhotoId);
        Assert.Null(user.Details?.BannerId);
    }

    [Fact]
    public async Task HandlerAsync_DeberiaGuardarLaClaveDeLaPrimeraFotoSubida()
    {
        var result = await SendToActualUserAsync(new UpdateUserRequest { Photo = TestData.File("primera.png") });

        Assert.Equal(ResultStatus.Ok, result.Status);
        var photoId = (await ReloadAsync()).Details?.PhotoId;
        Assert.NotNull(photoId);
        Assert.EndsWith(".png", photoId);
        Assert.Single(_storage.Objects(StorageHelper.BucketName));
    }

    [Fact]
    public async Task HandlerAsync_DeberiaGuardarLaClaveDeLaPrimeraPortadaSubida()
    {
        await SendToActualUserAsync(new UpdateUserRequest { Banner = TestData.File("primera.png") });

        Assert.NotNull((await ReloadAsync()).Details?.BannerId);
    }

    /// <summary>
    /// La clave nueva si se persiste, pero la clave anterior queda huerfana en RustFS.
    /// El handler no borra: encola la clave vieja para que <c>ImageCleanerService</c>
    /// la elimine en segundo plano.
    /// </summary>
    [Fact]
    public async Task HandlerAsync_DeberiaEncolarLaFotoAnteriorSinBorrarla()
    {
        await SendToActualUserAsync(new UpdateUserRequest { Photo = TestData.File("primera.png") });
        var primera = (await ReloadAsync()).Details!.PhotoId;
        Assert.NotNull(primera);
        Assert.Empty(await EnqueuedAsync());

        await SendToActualUserAsync(new UpdateUserRequest { Photo = TestData.File("segunda.png") });

        Assert.Equal([primera], await EnqueuedAsync());
        Assert.Equal(2, _storage.Objects(StorageHelper.BucketName).Count);
        Assert.Empty(_storage.DeletedKeys);
    }

    /// <inheritdoc cref="HandlerAsync_DeberiaEncolarLaFotoAnteriorSinBorrarla"/>
    [Fact]
    public async Task HandlerAsync_DeberiaEncolarLaPortadaAnteriorSinBorrarla()
    {
        await SendToActualUserAsync(new UpdateUserRequest { Banner = TestData.File("primera.png") });
        var primera = (await ReloadAsync()).Details!.BannerId;
        Assert.NotNull(primera);

        await SendToActualUserAsync(new UpdateUserRequest { Banner = TestData.File("segunda.png") });

        Assert.Equal([primera], await EnqueuedAsync());
        Assert.Empty(_storage.DeletedKeys);
    }

    [Fact]
    public async Task HandlerAsync_DeberiaEncolarFotoYPortadaJuntas()
    {
        await SendToActualUserAsync(new UpdateUserRequest { Photo = TestData.File("p1.png"), Banner = TestData.File("b1.png") });
        await SendToActualUserAsync(new UpdateUserRequest { Photo = TestData.File("p2.png"), Banner = TestData.File("b2.png") });

        Assert.Equal(2, (await EnqueuedAsync()).Length);
    }

    /// <summary>
    /// El requisito clave: la peticion no espera al borrado. <c>TryWrite</c> sobre un canal
    /// bounded nunca bloquea, ni aunque este lleno.
    /// </summary>
    [Fact]
    public async Task HandlerAsync_NoDeberiaBloquearAunqueElCanalEsteLleno()
    {
        var full = Channel.CreateBounded<string>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
        await full.Writer.WriteAsync("ocupado");
        var handler = new UpdateUserHandler(_scope.Context, _storage, full);

        await SendToActualUserAsync(new UpdateUserRequest { Photo = TestData.File("primera.png") });
        await SendToActualUserAsync(new UpdateUserRequest { Photo = TestData.File("segunda.png") });

        // Llego al ultimo await sin colgarse.
        Assert.Equal(ResultStatus.Ok, (await handler.HandlerAsync(new(await IdAsync(), new() { FirstName = "X" }), CancellationToken.None)).Status);
        Assert.Empty(_storage.DeletedKeys);
    }

    [Fact]
    public async Task HandlerAsync_NoDebeEncolarNadaSiNoHayImagenAnterior()
    {
        await SendToActualUserAsync(new UpdateUserRequest { Photo = TestData.File("primera.png") });

        Assert.Empty(await EnqueuedAsync());
    }

    [Fact]
    public async Task HandlerAsync_NoDebeEncolarNadaSiLaPeticionNoSubeArchivos()
    {
        await SendToActualUserAsync(new UpdateUserRequest { FirstName = "Forest" });

        Assert.Empty(await EnqueuedAsync());
    }

    [Fact]
    public async Task HandlerAsync_DeberiaActualizarElUserName()
    {
        var result = await SendToActualUserAsync(new UpdateUserRequest { UserName = "usuario-actualizado" });

        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.Equal("usuario-actualizado", (await ReloadAsync()).UserName);
    }

    [Fact]
    public async Task HandlerAsync_NoDebeCambiarElUserNameSiVieneVacio()
    {
        await SendToActualUserAsync(new UpdateUserRequest { UserName = "   " });

        Assert.Equal("usuario", (await ReloadAsync()).UserName);
    }

    /// <summary>
    /// Defecto conocido: <c>FirstName</c> y <c>LastName</c> se asignan a
    /// El nombre y el apellido se guardan en Details y no pisan el UserName.
    /// </summary>
    [Fact]
    public async Task HandlerAsync_NoDebeActualizarElUserNameAlCambiarElNombre()
    {
        var result = await SendToActualUserAsync(new UpdateUserRequest { FirstName = "Forest", LastName = "Nuevo" });

        Assert.Equal(ResultStatus.Ok, result.Status);
        var user = await ReloadAsync();
        Assert.Equal("usuario", user.UserName);
        Assert.Equal("Forest", user.Details?.FirstName);
        Assert.Equal("Nuevo", user.Details?.LastName);
    }

    [Fact]
    public async Task HandlerAsync_NoDebeCambiarElCorreoNiLaContrasena()
    {
        await SendToActualUserAsync(new UpdateUserRequest { Photo = TestData.File("nueva.png") });

        var user = await ReloadAsync();
        Assert.Equal("usuario@forest.dev", user.Security.Email);
        Assert.Equal(PasswordVerificationResult.Success,
            TestData.PasswordHasher.VerifyHashedPassword(default!, user.Security.Password, TestData.ValidPassword));
    }

    public void Dispose()
    {
        _scope.Dispose();
        _storage.Dispose();
        GC.SuppressFinalize(this);
    }
}