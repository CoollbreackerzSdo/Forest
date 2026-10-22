using System.Security.Claims;
using Forest.Minimal.Account.Handlers;
using Forest.Minimal.Account.Models;
using Forest.Minimal.Common.Endpoints.Abstractions;
using Forest.Minimal.Common.Endpoints.Filters;
using Forest.Minimal.Common.Handlers;
using Forest.Minimal.Common.Middlewares;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
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
        CancellationToken token)
    {
        if (!Guid.TryParse(claims.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id))
            return new Problem(StatusCodes.Status401Unauthorized, "Token inválido", "El token no contiene un identificador de usuario legible.");

        var result = await handler.HandlerAsync(id, token);

        return result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : new Problem(StatusCodes.Status404NotFound, "Recurso no encontrado", "No existe un registro con esos datos.");
    }

    private async Task<Results<Ok<Profile>, Problem>> Update(
        [FromForm] UpdateUserRequest request,
        ClaimsPrincipal claims,
        IHandler<UpdateUserQuery, Profile> handler,
        CancellationToken token)
    {
        if (!Guid.TryParse(claims.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id))
            return new Problem(StatusCodes.Status401Unauthorized, "Token inválido", "El token no contiene un identificador de usuario legible.");

        var result = await handler.HandlerAsync(new(id, request), token);

        if (result.IsSuccess) return TypedResults.Ok(result.Value);

        return new Problem(StatusCodes.Status404NotFound, "Recurso no encontrado", "No existe un registro con esos datos.");
    }
}
