namespace Forest.Minimal.Common.Endpoints.Abstractions;

public interface IEndpoint
{
    IEndpointRouteBuilder Map(IEndpointRouteBuilder builder);
}