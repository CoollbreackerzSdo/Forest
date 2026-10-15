using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Forest.Minimal.Account.Handlers;
using Forest.Minimal.Account.Services.Bearer.Models;

namespace Forest.Test.Aspire;

/// <summary>
/// Tests end-to-end contra el servidor publicado por el AppHost.
/// Requieren Docker (Postgres, Redis y RustFS reales).<para>
/// Para saltarlos: <c>dotnet test --filter "Category!=Docker"</c>.
/// </para>
/// </summary>
[Trait("Category", "Docker")]
public sealed class ForestServerTests(ForestAppFixture fixture) : IClassFixture<ForestAppFixture>
{
    private const string Password = "ForestCAFE123";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13];

    private HttpClient Client => fixture.Client;

    private static string Unique() => Guid.CreateVersion7().ToString("N")[..10];

    private static MultipartFormDataContent SignUpForm(
        string userName,
        string email,
        string password = Password,
        (string FileName, string ContentType, byte[] Content)? photo = null)
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent(userName), nameof(SignUpCommand.UserName) },
            { new StringContent(email), nameof(SignUpCommand.Email) },
            { new StringContent(password), nameof(SignUpCommand.Password) },
            { new StringContent("Forest"), nameof(SignUpCommand.FirstName) },
            { new StringContent("Integracion"), nameof(SignUpCommand.LastName) }
        };

        if (photo is { } file)
        {
            var content2 = new ByteArrayContent(file.Content);
            content2.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            content.Add(content2, nameof(SignUpCommand.Photo), file.FileName);
        }

        return content;
    }

    private async Task<(string UserName, string Email)> RegisterAsync()
    {
        var userName = Unique();
        var email = $"{userName}@forest.dev";
        using var response = await Client.PostAsync("auth/sign-up", SignUpForm(userName, email), CancellationToken.None);
        response.EnsureSuccessStatusCode();
        return (userName, email);
    }

    private async Task<BearerTransport> RegisterAndGetTokensAsync()
    {
        var userName = Unique();
        using var response = await Client.PostAsync("auth/sign-up", SignUpForm(userName, $"{userName}@forest.dev"), CancellationToken.None);
        response.EnsureSuccessStatusCode();

        var transport = await response.Content.ReadFromJsonAsync<BearerTransport>(Json, CancellationToken.None);
        return Assert.IsType<BearerTransport>(transport);
    }

    [Fact]
    public async Task HealthCheck_DeberiaResponderOk()
    {
        using var response = await Client.GetAsync("health/check", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SignUp_DeberiaRegistrarElUsuarioYDevolverLosTokens()
    {
        var userName = Unique();

        using var response = await Client.PostAsync("auth/sign-up", SignUpForm(userName, $"{userName}@forest.dev"), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var transport = await response.Content.ReadFromJsonAsync<BearerTransport>(Json, CancellationToken.None);
        Assert.NotNull(transport);
        Assert.NotEmpty(transport.Token);
        Assert.NotEmpty(transport.RefreshToken);
        Assert.True(transport.Exp > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task SignUp_ConCorreoDuplicado_DeberiaDevolverConflicto()
    {
        var email = $"{Unique()}@forest.dev";
        using var first = await Client.PostAsync("auth/sign-up", SignUpForm(Unique(), email), CancellationToken.None);
        first.EnsureSuccessStatusCode();

        using var second = await Client.PostAsync("auth/sign-up", SignUpForm(Unique(), email), CancellationToken.None);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Theory]
    [InlineData("no-es-un-correo")]
    [InlineData("")]
    public async Task SignUp_ConDatosInvalidos_DeberiaDevolverBadRequest(string email)
    {
        using var response = await Client.PostAsync("auth/sign-up", SignUpForm(Unique(), email), CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SignUp_ConContrasenaInvalida_DeberiaDevolverBadRequest()
    {
        var userName = Unique();

        using var response = await Client.PostAsync("auth/sign-up", SignUpForm(userName, $"{userName}@forest.dev", "corta"), CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SignUp_ConArchivoDeTipoNoPermitido_DeberiaDevolverBadRequest()
    {
        var userName = Unique();
        var photo = ("malicioso.exe", "application/octet-stream", new byte[] { 1, 2, 3 });

        using var response = await Client.PostAsync("auth/sign-up", SignUpForm(userName, $"{userName}@forest.dev", Password, photo), CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SignUp_ConFotoValida_DeberiaAceptarElRegistro()
    {
        var userName = Unique();
        var photo = ("foto.png", "image/png", PngBytes);

        using var response = await Client.PostAsync("auth/sign-up", SignUpForm(userName, $"{userName}@forest.dev", Password, photo), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SignIn_ConCredencialesValidas_DeberiaDevolverToken()
    {
        var (_, email) = await RegisterAsync();

        using var response = await Client.PostAsJsonAsync("auth/sign-in", new SignInRequest(email, Password), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var transport = await response.Content.ReadFromJsonAsync<BearerTransport>(Json, CancellationToken.None);
        Assert.NotEmpty(transport!.Token);
    }

    [Fact]
    public async Task SignIn_ConContrasenaIncorrecta_DeberiaDevolverBadRequest()
    {
        var (_, email) = await RegisterAsync();

        using var response = await Client.PostAsJsonAsync("auth/sign-in", new SignInRequest(email, "Incorrecta123"), CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SignIn_ConUsuarioInexistente_DeberiaDevolverBadRequest()
    {
        using var response = await Client.PostAsJsonAsync("auth/sign-in", new SignInRequest("nadie@forest.dev", Password), CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_SinToken_DeberiaDevolverUnauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "users") { Content = new MultipartFormDataContent() };

        using var response = await Client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_ConTokenValido_DeberiaActualizarElUserName()
    {
        var transport = await RegisterAndGetTokensAsync();
        var content = new MultipartFormDataContent { { new StringContent(Unique()), nameof(UpdateUserRequest.UserName) } };
        using var request = new HttpRequestMessage(HttpMethod.Put, "users") { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", transport.Token);

        using var response = await Client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_ConTokenInvalido_DeberiaDevolverUnauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "users") { Content = new MultipartFormDataContent() };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "no-es-un-token-valido");

        using var response = await Client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DownloadImage_DeberiaDevolverNotFoundParaUnaClaveInexistente()
    {
        using var response = await Client.GetAsync($"img/{Guid.CreateVersion7()}.png", CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}