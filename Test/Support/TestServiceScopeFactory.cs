using Microsoft.Extensions.DependencyInjection;

namespace Forest.Test.Support;

/// <summary>
/// <see cref="IServiceScopeFactory"/> minimo para poder construir a mano los servicios
/// que crean un scope por operacion, como <c>ImageCleanerService</c>.
/// </summary>
public sealed class TestServiceScopeFactory(IServiceProvider services) : IServiceScopeFactory
{
    public IServiceScope CreateScope() => new TestServiceScope(services);

    public void Dispose() { }

    private sealed class TestServiceScope(IServiceProvider services) : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = services;
        public void Dispose() { }
    }
}