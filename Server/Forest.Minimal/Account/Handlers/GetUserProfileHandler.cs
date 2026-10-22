using Forest.Minimal.Account.Models;
using Forest.Minimal.Common.Handlers;

namespace Forest.Minimal.Account.Handlers;

public sealed class GetUserProfileHandler(ForestContext context) : IHandler<EntityId, Profile>
{
    public async Task<Result<Profile>> HandlerAsync(EntityId request, CancellationToken token)
    {
        var user = await context.Users
            .Where(x => x.Key == request)
            .Select(x => new Profile(x.Security.Email, x.UserName, x.Details!.FirstName, x.Details!.LastName, x.Details!.PhotoId, x.Details!.BannerId))
            .FirstOrDefaultAsync(token);

        return user is null ? Result.NotFound() : Result.Success(user);
    }
}