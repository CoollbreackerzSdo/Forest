using System.Security.Claims;
using Forest.Minimal.Account.Handlers;
using Forest.Minimal.Account.Services.Bearer.Models;
using Forest.Minimal.Common.Endpoints.Abstractions;
using Forest.Minimal.Common.Endpoints.Filters;
using Forest.Minimal.Common.Handlers;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Forest.Minimal.Account.Endpoints;

public sealed class SignAuthEndpoint : IEndpoint
{
    public IEndpointRouteBuilder Map(IEndpointRouteBuilder builder)
    {
        var endp = builder.MapGroup("auth").WithTags("Auth");

        endp.MapPost("sign-up", SignUp)
            .Accepts<SignUpCommand>("multipart/form-data")
            .AddEndpointFilter<ValidationFilter<SignUpCommand>>()
            .Produces<BearerTransport>();

        endp.MapPost("sign-in", SignIn)
            .Accepts<SignInRequest>("application/json")
            .AddEndpointFilter<ValidationFilter<SignInRequest>>()
            .Produces<BearerTransport>();

        endp.MapPost("sign-up-web", SignUpWeb)
            .Accepts<SignUpCommand>("multipart/form-data")
            .AddEndpointFilter<ValidationFilter<SignUpCommand>>()
            .Produces(StatusCodes.Status200OK);

        return endp;
    }

    private async Task<Results<Ok, BadRequest, NotFound>> SignUpWeb([FromForm] SignUpCommand command, HttpContext context, IHandler<SignUpCommand, BearerTransport> handler, CancellationToken token)
    {
        var result = await handler.HandlerAsync(command, token);
        context.Response.Cookies.Append(
            AuthHelper.TokenCookieParameter,
            result.Value.Token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = result.Value.Exp,
                Path = "/"
            });

        return TypedResults.Ok();
    }

    private async Task<Results<Ok<BearerTransport>, NotFound>> SignUp([FromForm] SignUpCommand command, IHandler<SignUpCommand, BearerTransport> handler, CancellationToken token)
    {
        var result = await handler.HandlerAsync(command, token);
        return TypedResults.Ok(result.Value);
    }

    private async Task<Results<Ok<BearerTransport>, BadRequest>> SignIn(SignInRequest command, IHandler<SignInRequest, BearerTransport> handler, CancellationToken token)
    {
        var result = await handler.HandlerAsync(command, token);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : TypedResults.BadRequest();
    }
}
