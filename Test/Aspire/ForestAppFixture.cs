using Aspire.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Forest.Test.Aspire;

/// <summary>
/// Levanta el AppHost completo (Postgres + Redis + RustFS + Forest.Minimal) una sola vez
/// para todos los tests de la clase. <para><b>Requiere Docker</b>: sin el demonio de Docker
/// Docker Desktop los tests fallan en <see cref="InitializeAsync"/>.</para>
/// </summary>
public sealed class ForestAppFixture : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    /// <summary>Clave simetrica usada para firmar los tokens en los tests.</summary>
    public const string BearerKey = "forest-integration-test-bearer-key-0123456789";

    private static readonly Dictionary<string, string> Parameters = new()
    {
        ["BearerKey"] = BearerKey,
        ["AccessKey"] = "forestadmin",
        ["SecretKey"] = "forestsecret0123456789"
    };

    public DistributedApplication? Application { get; private set; }

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Forest_Host>();

        // Valores deterministas para los parametros secretos, que si no Aspire pediria por consola.
        // ParameterResource.ConfigurationKey usa el prefijo "parameters:".
        foreach (var (key, value) in Parameters)
        {
            builder.Configuration[$"parameters:{key}"] = value;
            Environment.SetEnvironmentVariable($"parameters__{key}", value);
        }

        builder.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Warning);
            logging.AddFilter(builder.Environment.ApplicationName, LogLevel.Warning);
        });
        builder.Services.ConfigureHttpClientDefaults(client => client.AddStandardResilienceHandler());

        Application = await builder.BuildAsync();
        await Application.StartAsync(WaitAsync(Timeout));

        Client = Application.CreateHttpClient("forest-server");

        await Application.ResourceNotifications
            .WaitForResourceHealthyAsync("forest-database", WaitAsync(Timeout));
        await Application.ResourceNotifications
            .WaitForResourceHealthyAsync("forest-cache", WaitAsync(Timeout));
        await Application.ResourceNotifications
            .WaitForResourceHealthyAsync("forest-s3", WaitAsync(Timeout));

        // El servidor ejecuta MigrateAsync y LoadBucketAsync sin esperar (fire-and-forget),
        // asi que hay que esperar a que el esquema exista antes de golpear los endpoints.
        await WaitForServerAsync();
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (Application is not null)
            await Application.DisposeAsync();
    }

    private async Task WaitForServerAsync()
    {
        var deadline = DateTime.UtcNow + Timeout;
        Exception? last = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var response = await Client.GetAsync("health/check", WaitAsync(Timeout));
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch (Exception exception)
            {
                last = exception;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        throw new TimeoutException($"El servidor 'forest-server' no respondio en {Timeout}.", last);
    }

    private static CancellationToken WaitAsync(TimeSpan timeout)
    {
        var source = new CancellationTokenSource(timeout);
        return source.Token;
    }
}