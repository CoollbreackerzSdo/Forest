using Forest.Minimal.Account.Handlers;
using Forest.Minimal.Account.Services.Bearer.Models;
using Forest.Minimal.Common.Endpoints.Abstractions;
using Forest.Minimal.Common.Endpoints.Filters;
using Forest.Minimal.Common.Handlers;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Forest.Minimal.Account.Endpoints;

public sealed class SignAuthEndpoint : IEndpoint
{
    public IEndpointRouteBuilder Map(IEndpointRouteBuilder builder)
    {
        var endp = builder.MapGroup("auth").WithTags("Auth");

        endp.MapPost("sign-up", SignUp)
            .Accepts<SignUpCommand>("application/json")
            .AddEndpointFilter<ValidationFilter<SignUpCommand>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces<BearerTransport>();

        return endp;
    }

    private async Task<Results<Ok<BearerTransport>, NotFound>> SignUp(SignUpCommand command, IHandler<SignUpCommand, BearerTransport> handler, CancellationToken token)
    {
        var result = await handler.HandlerAsync(command, token);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : TypedResults.NotFound();
    }
}