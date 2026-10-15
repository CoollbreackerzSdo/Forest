using Aspire.Hosting;
using Aspire.Hosting.Postgres;
using Forest.Host.Rustfs.Resources;

namespace Forest.Test.Aspire;

/// <summary>
/// Verifica el modelo de recursos declarado por el AppHost (<c>Forest.Host</c>).
/// No arranca contenedores: solo ejecuta <c>DistributedApplicationTestingBuilder.BuildAsync()</c>,
/// por lo que corre sin Docker.
/// </summary>
public sealed class AppHostResourceTests : IAsyncLifetime
{
    private DistributedApplication? _application;
    private DistributedApplicationModel? _model;

    public async Task InitializeAsync()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Forest_Host>();
        _application = await builder.BuildAsync();
        _model = _application.Services.GetRequiredService<DistributedApplicationModel>();
    }

    public async Task DisposeAsync()
    {
        if (_application is not null)
            await _application.DisposeAsync();
    }

    private IResource Resource(string name) => Assert.Single(_model!.Resources, x => x.Name == name);

    private EndpointAnnotation Endpoint(string resource, string name)
        => Assert.Single(Resource(resource).Annotations.OfType<EndpointAnnotation>(), x => x.Name == name);

    private string[] RelatedResources(IResource resource)
        => resource.Annotations.OfType<ResourceRelationshipAnnotation>().Select(x => x.Resource?.Name ?? string.Empty).ToArray();

    [Fact]
    public void DeberiaDeclararLaBaseDeDatosPostgres()
    {
        Assert.IsType<PostgresServerResource>(Resource("forest-database"));

        var tcp = Endpoint("forest-database", "tcp");
        Assert.Equal(5610, tcp.Port);
        Assert.Equal(5432, tcp.TargetPort);
    }

    [Fact]
    public void DeberiaDeclararPgAdminContraLaBaseDeDatos()
    {
        Assert.IsType<PgAdminContainerResource>(Resource("forest-pg"));
        Assert.Equal(5611, Endpoint("forest-pg", "http").Port);
    }

    [Fact]
    public void DeberiaDeclararLaCacheRedis()
    {
        Assert.IsType<RedisResource>(Resource("forest-cache"));

        var tcp = Endpoint("forest-cache", "tcp");
        Assert.Equal(5612, tcp.Port);
        Assert.Equal(6379, tcp.TargetPort);
    }

    [Fact]
    public void DeberiaDeclararElAlmacenamientoRustfs()
    {
        Assert.IsType<RustfsResource>(Resource("forest-s3"));

        var api = Endpoint("forest-s3", "default");
        Assert.Equal(5613, api.Port);
        Assert.Equal(9000, api.TargetPort);

        var console = Endpoint("forest-s3", "console");
        Assert.Equal(5614, console.Port);
        Assert.Equal(9001, console.TargetPort);
    }

    [Fact]
    public void DeberiaDeclararElServidorConEndpointsHttpYHttps()
    {
        Assert.IsType<ProjectResource>(Resource("forest-server"));
        Assert.Equal(5620, Endpoint("forest-server", "http").Port);
        Assert.Equal(5621, Endpoint("forest-server", "https").Port);
    }

    [Fact]
    public void DeberiaDeclararLosParametrosSecretos()
    {
        var parameters = _model!.Resources.OfType<ParameterResource>().ToArray();

        Assert.Equal(["AccessKey", "BearerKey", "SecretKey"], parameters.Select(x => x.Name).Order().ToArray());
        Assert.All(parameters, x => Assert.True(x.Secret, $"El parametro '{x.Name}' deberia ser secreto."));
    }

    [Fact]
    public void ElServidorDeberiaEsperarAElArranqueDeLosContenedores()
    {
        var waited = Resource("forest-server").Annotations
            .OfType<WaitAnnotation>()
            .Select(x => x.Resource?.Name ?? string.Empty)
            .ToArray();

        Assert.Contains("forest-database", waited);
        Assert.Contains("forest-cache", waited);
        Assert.Contains("forest-s3", waited);
    }

    [Fact]
    public void ElServidorDeberiaReferenciarLaBaseDeDatosLaCacheYElAlmacenamiento()
    {
        var referenced = RelatedResources(Resource("forest-server"));

        Assert.Contains("forest-database", referenced);
        Assert.Contains("forest-cache", referenced);
        Assert.Contains("forest-s3", referenced);
    }

    [Fact]
    public void ElServidorDeberiaRecibirLaClaveYLasCredencialesComoParametros()
    {
        var referenced = RelatedResources(Resource("forest-server"));

        Assert.Contains("BearerKey", referenced);
        Assert.Contains("AccessKey", referenced);
        Assert.Contains("SecretKey", referenced);
    }

    [Fact]
    public void LosContenedoresDeberianMontarElVolumenPersistenteForest()
    {
        var expected = new Dictionary<string, string>
        {
            ["forest-database"] = "/var/lib/postgres",
            ["forest-cache"] = "/data",
            ["forest-s3"] = "/data"
        };

        foreach (var (resource, target) in expected)
        {
            var container = Assert.IsAssignableFrom<ContainerResource>(Resource(resource));
            var mount = Assert.Single(container.Annotations.OfType<ContainerMountAnnotation>(), x => x.Target == target);
            Assert.Equal("forest", mount.Source);
            Assert.Equal(ContainerMountType.Volume, mount.Type);
        }
    }
}