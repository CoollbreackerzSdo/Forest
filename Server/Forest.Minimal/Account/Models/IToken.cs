using Forest.Minimal.Common.Models;

namespace Forest.Minimal.Account.Models;

public interface IToken
{
    EntityId Key { get; }
    EntityId RefreshId { get; }
    DateTimeOffset Exp { get; }
    DateTimeOffset RefreshExp { get; }
}

public sealed record TokenModel(EntityId Key, EntityId RefreshId, DateTimeOffset Exp, DateTimeOffset RefreshExp, EntityId UserId) : IEntity<EntityId>, IToken;