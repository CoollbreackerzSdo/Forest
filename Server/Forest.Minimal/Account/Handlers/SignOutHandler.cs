using Forest.Minimal.Common.Handlers;

namespace Forest.Minimal.Account.Handlers;

public sealed class SignOutHandler(ForestContext context) : IRequestHandler<EntityId>
{
    public async Task<Result> HandlerAsync(EntityId request, CancellationToken token)
        => await context.Tokens.Where(x => x.Key == request).ExecuteDeleteAsync(token) > 0 ? Result.Success() : Result.NotFound();
}