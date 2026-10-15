using Forest.Minimal.Common.Models;

namespace Forest.Minimal.Account.Models;

public interface IAccount : IEntity<EntityId>
{
    string UserName { get; }
    Details? Details { get; }
    Security Security { get; }
}
