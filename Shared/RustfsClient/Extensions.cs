using Microsoft.Extensions.DependencyInjection;
using RustfsClient.Abstractions;
using RustfsClient.Clients;
using RustfsClient.Options;

namespace RustfsClient;

public static class RustfsClientServiceCollectionExtensions
{
    public static IServiceCollection AddRustfsClient(this IServiceCollection services, RustfsClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        services.AddSingleton(options);
        services.AddSingleton<IRustfsObjectStorageClient>(serviceProvider => new RustfsObjectStorageClient(serviceProvider.GetRequiredService<RustfsClientOptions>()));
        return services;
    }
}