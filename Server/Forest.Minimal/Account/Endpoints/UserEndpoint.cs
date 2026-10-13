using System.Security.Claims;
using Forest.Minimal.Account.Handlers;
using Forest.Minimal.Common.Endpoints.Abstractions;
using Forest.Minimal.Common.Endpoints.Filters;
using Forest.Minimal.Common.Handlers;
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
            .AddEndpointFilter<ValidationFilter<UpdateUserRequest>>();

        return endp;
    }

    private async Task<Results<Ok, NotFound, UnauthorizedHttpResult>> Update([FromForm] UpdateUserRequest request, ClaimsPrincipal claims, IRequestHandler<UpdateUserQuery> handler, CancellationToken token)
    {
        if (Guid.TryParse(claims.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id))
        {
            var result = await handler.HandlerAsync(new(id, request), token);
            return result.IsSuccess ? TypedResults.Ok() : TypedResults.NotFound();
        }
        return TypedResults.Unauthorized();
    }
}