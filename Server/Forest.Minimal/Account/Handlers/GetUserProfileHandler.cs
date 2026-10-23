using Forest.Minimal.Account.Models;
using Forest.Minimal.Common.Handlers;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace Forest.Minimal.Account.Handlers;

public sealed class GetUserProfileHandler(ForestContext context) : IHandler<EntityId, Profile>
{
    public async Task<Result<Profile>> HandlerAsync(EntityId request, CancellationToken token)
    {
        var user = await (
            from x in context.Users
            where x.Key == request
            select new Profile(x.Security.Email, x.UserName, x.Details!.FirstName, x.Details!.LastName, x.Details!.PhotoId, x.Details!.BannerId)
            ).FirstOrDefaultAsync(token);

        return user is null ? Result.NotFound() : Result.Success(user);
    }
}