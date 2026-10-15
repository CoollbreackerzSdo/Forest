using Forest.Minimal.Account.Handlers;
using Forest.Minimal.Account.Services.Bearer.Generator;
using Forest.Test.Support;
using Microsoft.AspNetCore.Identity;

namespace Forest.Test.Unit.Account;

public sealed class SignInHandlerTests : IDisposable
{
    private readonly ForestContextScope _scope = ForestContextScope.Create();
    private readonly SignInHandler _handler;

    public SignInHandlerTests()
    {
        TestData.SeedUser(_scope.Context, "usuario", "usuario@forest.dev");
        _handler = new SignInHandler(TestData.PasswordHasher, _scope.Context, new BearerGenerator(TestData.BearerConfiguration));
    }

    [Fact]
    public async Task HandlerAsync_DeberiaIniciarSesionConCredencialesValidas()
    {
        var result = await _handler.HandlerAsync(new SignInRequest("usuario@forest.dev", TestData.ValidPassword), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value.Token);
        Assert.NotEmpty(result.Value.RefreshToken);
    }

    [Fact]
    public async Task HandlerAsync_DeberiaRegistrarUnTokenNuevoEnCadaInicioDeSesion()
    {
        var result = await _handler.HandlerAsync(new SignInRequest("usuario@forest.dev", TestData.ValidPassword), CancellationToken.None);

        var token = Assert.Single(await _scope.Context.Tokens.ToListAsync());
        var user = await _scope.Context.Users.SingleAsync();
        Assert.Equal(user.Key, token.UserId);
        Assert.Equal(result.Value.Exp, token.Exp);
    }

    [Fact]
    public async Task HandlerAsync_DeberiaRechazarUsuariosInexistentes()
    {
        var result = await _handler.HandlerAsync(new SignInRequest("nadie@forest.dev", TestData.ValidPassword), CancellationToken.None);

        Assert.Equal(ResultStatus.NoContent, result.Status);
        Assert.Empty(await _scope.Context.Tokens.ToListAsync());
    }

    [Fact]
    public async Task HandlerAsync_DeberiaRechazarContrasenaIncorrecta()
    {
        var result = await _handler.HandlerAsync(new SignInRequest("usuario@forest.dev", "Incorrecta123"), CancellationToken.None);

        Assert.Equal(ResultStatus.Unauthorized, result.Status);
        Assert.Empty(await _scope.Context.Tokens.ToListAsync());
    }

    [Fact]
    public async Task HandlerAsync_NoDebePersistirTokenSiLaContrasenaFalla()
    {
        await _handler.HandlerAsync(new SignInRequest("usuario@forest.dev", "Incorrecta123"), CancellationToken.None);

        Assert.Empty(await _scope.Context.Tokens.ToListAsync());
    }

    [Fact]
    public async Task HandlerAsync_DeberiaEmitirUnTokenConElIdentificadorDelUsuario()
    {
        var result = await _handler.HandlerAsync(new SignInRequest("usuario@forest.dev", TestData.ValidPassword), CancellationToken.None);
        var user = await _scope.Context.Users.SingleAsync();

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(result.Value.Token);

        Assert.Equal(user.Key.Value.ToString(), token.Subject);
        Assert.Equal("usuario@forest.dev", token.Claims.Single(x => x.Type == "email").Value);
    }

    public void Dispose()
    {
        _scope.Dispose();
        GC.SuppressFinalize(this);
    }
}