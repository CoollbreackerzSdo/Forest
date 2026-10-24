using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Forest.Minimal.Account.Handlers;
using Forest.Minimal.Account.Models;
using Forest.Minimal.Common.Endpoints.Abstractions;
using Forest.Minimal.Common.Endpoints.Filters;
using Forest.Minimal.Common.Handlers;
using Forest.Minimal.Common.Middlewares;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Forest.Minimal.Account.Endpoints;

public sealed class UserEndpoint : IEndpoint
{
    public IEndpointRouteBuilder Map(IEndpointRouteBuilder builder)
    {
        var endp = builder.MapGroup("users").WithTags("Account").RequireAuthorization(AuthHelper.DefaultPolicy);

        endp.MapPut("", Update)
            .Accepts<UpdateUserRequest>("multipart/form-data")
            .AddEndpointFilter<ValidationFilter<UpdateUserRequest>>()
            .Produces<Profile>(StatusCodes.Status200OK)
            .Produces<Problem>(StatusCodes.Status400BadRequest, Problem.ContentType)
            .Produces<Problem>(StatusCodes.Status401Unauthorized, Problem.ContentType)
            .Produces<Problem>(StatusCodes.Status404NotFound, Problem.ContentType);

        endp.MapGet("", Get)
            .Produces<Profile>(StatusCodes.Status200OK)
            .Produces<Problem>(StatusCodes.Status401Unauthorized, Problem.ContentType)
            .Produces<Problem>(StatusCodes.Status404NotFound, Problem.ContentType);

        return endp;
    }

    private async Task<Results<Ok<Profile>, Problem>> Get(
        ClaimsPrincipal claims,
        IHandler<EntityId, Profile> handler,
        IDistributedCache cache,
        CancellationToken token)
    {
        var key = claims.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(key, out var id))
            return new Problem(StatusCodes.Status401Unauthorized, "Token inválido", "El token no contiene un identificador de usuario legible.");

        if (cache.TryGet<Profile>(key, out var value))
            return TypedResults.Ok(value);

        var result = await handler.HandlerAsync(id, token);

        await cache.SetAsync(key, result.Value, TimeSpan.FromHours(1), token);

        return result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : new Problem(StatusCodes.Status404NotFound, "Recurso no encontrado", "No existe un registro con esos datos.");
    }

    private async Task<Results<Ok<Profile>, Problem>> Update(
        [FromForm] UpdateUserRequest request,
        ClaimsPrincipal claims,
        IDistributedCache cache,
        IHandler<UpdateUserQuery, Profile> handler,
        CancellationToken token)
    {
        var key = claims.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(key, out var id))
            return new Problem(StatusCodes.Status401Unauthorized, "Token inválido", "El token no contiene un identificador de usuario legible.");

        var result = await handler.HandlerAsync(new(id, request), token);

        if (result.IsSuccess)
        {
            await cache.SetAsync(key, result.Value, TimeSpan.FromHours(1), token);
            return TypedResults.Ok(result.Value);
        }

        return new Problem(StatusCodes.Status404NotFound, "Recurso no encontrado", "No existe un registro con esos datos.");
    }
}
