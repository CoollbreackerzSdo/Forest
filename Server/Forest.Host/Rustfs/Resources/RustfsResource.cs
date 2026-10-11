namespace Forest.Host.Rustfs.Resources;

public sealed class RustfsResource(string name, string? entrypoint = null) : ContainerResource(name, entrypoint), IResourceWithServiceDiscovery
{
    public EndpointReference HttpEndpoint => field ??= new EndpointReference(this, HttpEndpointName);
    public EndpointReference HttpConsoleEndpoint => field ??= new EndpointReference(this, HttpConsoleEndpointName);
    internal const string HttpEndpointName = "default";
    internal const string HttpConsoleEndpointName = "console";
}