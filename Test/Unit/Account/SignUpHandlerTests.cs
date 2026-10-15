using Forest.Minimal.Account.Handlers;
using Forest.Minimal.Account.Services.Bearer.Generator;
using Forest.Test.Support;
using Microsoft.AspNetCore.Identity;

namespace Forest.Test.Unit.Account;

public sealed class SignUpHandlerTests : IDisposable
{
    private readonly ForestContextScope _scope = ForestContextScope.Create();
    private readonly FakeRustfsObjectStorageClient _storage = new();
    private readonly SignUpHandler _handler;

    public SignUpHandlerTests()
    {
        _handler = new SignUpHandler(
            TestData.PasswordHasher,
            _scope.Context,
            new BearerGenerator(TestData.BearerConfiguration),
            _storage);
    }

    private static SignUpCommand Command(string email = "usuario@forest.dev", string userName = "usuario", string password = TestData.ValidPassword)
        => new(email, userName, password) { FirstName = "Forest", LastName = "Test" };

    [Fact]
    public async Task HandlerAsync_DeberiaPersistirElUsuarioYSuToken()
    {
        var result = await _handler.HandlerAsync(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var user = Assert.Single(await _scope.Context.Users.ToListAsync());
        Assert.Equal("usuario", user.UserName);
        Assert.Equal("usuario@forest.dev", user.Security.Email);
        Assert.Equal("Forest", user.Details?.FirstName);
        Assert.True(user.IsEnable);
    }

    [Fact]
    public async Task HandlerAsync_DeberiaGuardarLaContrasenaHasheada()
    {
        await _handler.HandlerAsync(Command(), CancellationToken.None);

        var user = await _scope.Context.Users.SingleAsync();
        Assert.NotEqual(TestData.ValidPassword, user.Security.Password);
        Assert.Equal(PasswordVerificationResult.Success, TestData.PasswordHasher.VerifyHashedPassword(default!, user.Security.Password, TestData.ValidPassword));
    }

    [Fact]
    public async Task HandlerAsync_DeberiaRegistrarElTokenVinculadoAlUsuario()
    {
        var result = await _handler.HandlerAsync(Command(), CancellationToken.None);

        var user = await _scope.Context.Users.SingleAsync();
        var token = Assert.Single(await _scope.Context.Tokens.ToListAsync());
        Assert.Equal(user.Key, token.UserId);
        Assert.Equal(result.Value.Exp, token.Exp);
        Assert.Equal(result.Value.RefreshExp, token.RefreshExp);
    }

    [Fact]
    public async Task HandlerAsync_NoDebeSubirArchivosSiNoSeProporcionan()
    {
        await _handler.HandlerAsync(Command(), CancellationToken.None);

        var user = await _scope.Context.Users.SingleAsync();
        Assert.Empty(_storage.Objects(StorageHelper.BucketName));
        Assert.Null(user.Details?.PhotoId);
        Assert.Null(user.Details?.BannerId);
    }

    [Fact]
    public async Task HandlerAsync_DeberiaSubirFotoYPortadaYGuardarSusClaves()
    {
        var command = Command() with { Photo = TestData.File("foto.png"), Banner = TestData.File("portada.png") };

        await _handler.HandlerAsync(command, CancellationToken.None);

        var user = await _scope.Context.Users.SingleAsync();
        var objects = _storage.Objects(StorageHelper.BucketName);
        Assert.NotNull(user.Details?.PhotoId);
        Assert.NotNull(user.Details?.BannerId);
        Assert.Contains(user.Details!.PhotoId, objects.Keys);
        Assert.Contains(user.Details!.BannerId, objects.Keys);
        Assert.Equal(2, objects.Count);
    }

    [Fact]
    public async Task HandlerAsync_DeberiaGenerarClavesDeArchivoConExtensionYVersion7()
    {
        var command = Command() with { Photo = TestData.File("foto.png") };

        await _handler.HandlerAsync(command, CancellationToken.None);

        var user = await _scope.Context.Users.SingleAsync();
        Assert.EndsWith(".png", user.Details!.PhotoId);
        Assert.True(Guid.TryParse(Path.GetFileNameWithoutExtension(user.Details.PhotoId), out _));
    }

    public void Dispose()
    {
        _scope.Dispose();
        _storage.Dispose();
        GC.SuppressFinalize(this);
    }
}