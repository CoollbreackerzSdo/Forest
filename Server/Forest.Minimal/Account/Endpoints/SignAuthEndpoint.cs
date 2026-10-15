using System.Security.Claims;
using Forest.Minimal.Account.Handlers;
using Forest.Minimal.Account.Services.Bearer.Models;
using Forest.Minimal.Common.Endpoints.Abstractions;
using Forest.Minimal.Common.Endpoints.Filters;
using Forest.Minimal.Common.Handlers;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Forest.Minimal.Account.Endpoints;

public sealed class SignAuthEndpoint : IEndpoint
{
    public IEndpointRouteBuilder Map(IEndpointRouteBuilder builder)
    {
        var endp = builder.MapGroup("auth").WithTags("Auth");

        endp.MapPost("sign-up", SignUp)
            .Accepts<SignUpCommand>("multipart/form-data")
            .AddEndpointFilter<ValidationFilter<SignUpCommand>>()
            .Produces<BearerTransport>()
            .Produces<Problem>(StatusCodes.Status400BadRequest, Problem.ContentType)
            .Produces<Problem>(StatusCodes.Status409Conflict, Problem.ContentType);

        endp.MapPost("sign-in", SignIn)
            .Accepts<SignInRequest>("application/json")
            .AddEndpointFilter<ValidationFilter<SignInRequest>>()
            .Produces<BearerTransport>()
            .Produces<Problem>(StatusCodes.Status400BadRequest, Problem.ContentType)
            .Produces<Problem>(StatusCodes.Status404NotFound, Problem.ContentType);

        endp.MapPost("sign-up-web", SignUpWeb)
            .Accepts<SignUpCommand>("multipart/form-data")
            .AddEndpointFilter<ValidationFilter<SignUpCommand>>()
            .Produces(StatusCodes.Status200OK)
            .Produces<Problem>(StatusCodes.Status400BadRequest, Problem.ContentType)
            .Produces<Problem>(StatusCodes.Status409Conflict, Problem.ContentType);

        endp.MapGet("sign-out-web", SignOutWeb)
            .RequireAuthorization(AuthHelper.DefaultPolicy)
            .Produces(StatusCodes.Status200OK);

        endp.MapGet("sign-out", SignOut)
        .RequireAuthorization(AuthHelper.DefaultPolicy)
        .Produces(StatusCodes.Status200OK);


        return endp;
    }

    private async Task<Ok> SignOut(ClaimsPrincipal claims, [FromKeyedServices("sign-out")] IRequestHandler<EntityId> handler, CancellationToken token)
    {
        await handler.HandlerAsync(Guid.Parse(claims.FindFirstValue(JwtRegisteredClaimNames.Jti)!), token);
        return TypedResults.Ok();
    }


    private async Task<Ok> SignOutWeb(HttpContext context, ClaimsPrincipal claims, [FromKeyedServices("sign-out")] IRequestHandler<EntityId> handler, CancellationToken token)
    {
        context.Response.Cookies.Delete(AuthHelper.TokenCookieParameter, new CookieOptions()
        {
            Expires = DateTimeOffset.UtcNow,
            Path = "/",
            SameSite = SameSiteMode.Lax,
        });

        await handler.HandlerAsync(Guid.Parse(claims.FindFirstValue(JwtRegisteredClaimNames.Jti)!), token);

        return TypedResults.Ok();
    }


    private async Task<Results<Ok, Problem>> SignUpWeb([FromForm] SignUpCommand command, HttpContext context, IHandler<SignUpCommand, BearerTransport> handler, CancellationToken token)
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

    private async Task<Results<Ok<BearerTransport>, Problem>> SignUp([FromForm] SignUpCommand request, IHandler<SignUpCommand, BearerTransport> handler, CancellationToken token)
    {
        var result = await handler.HandlerAsync(request, token);
        return TypedResults.Ok(result.Value);
    }

    private async Task<Results<Ok<BearerTransport>, Problem>> SignIn(SignInRequest request, IHandler<SignInRequest, BearerTransport> handler, CancellationToken token)
    {
        var result = await handler.HandlerAsync(request, token);
        return result.Status switch
        {
            ResultStatus.Ok => TypedResults.Ok(result.Value),
            ResultStatus.NotFound or ResultStatus.NoContent => new Problem(StatusCodes.Status404NotFound, "Recurso no encontrado", "No existe un registro con esos datos."),
            _ => new Problem(StatusCodes.Status400BadRequest, "Credenciales inválidas", "El correo o la contraseña no son correctos.")
        };
    }
}