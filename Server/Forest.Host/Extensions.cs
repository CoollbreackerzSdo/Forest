using Forest.Host.Rustfs.Helpers;
using Forest.Host.Rustfs.Resources;

namespace Forest.Host;

public static partial class Extensions
{
    extension(IDistributedApplicationBuilder builder)
    {
        public IResourceBuilder<RustfsResource> AddRustfs(string name, IResourceBuilder<ParameterResource> accessKey, IResourceBuilder<ParameterResource> secretKey, int port = 9000, int consolePort = 9001)
        {
            var resource = new RustfsResource(name);
            return builder.AddResource(resource)
                .WithImage(ContainerImageTags.Image)
                .WithImageRegistry(ContainerImageTags.Registry)
                .WithImageTag(ContainerImageTags.Tag)
                .WithHttpEndpoint(name: resource.HttpEndpoint.EndpointName, port: port, targetPort: 9000)
                .WithHttpEndpoint(name: resource.HttpConsoleEndpoint.EndpointName, port: consolePort, targetPort: 9001)
                .WithEnvironment("RUSTFS_ACCESS_KEY", accessKey)
                .WithEnvironment("RUSTFS_SECRET_KEY", secretKey)
                .WithEnvironment("RUSTFS_ADDRESS", ":9000")
                .WithEnvironment("RUSTFS_CONSOLE_ENABLE", "true")
                .WithArgs("/data");
        }
    }
}