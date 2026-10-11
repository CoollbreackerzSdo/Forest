namespace Forest.Minimal.Common.Models;

public abstract class EntityBase : IEntity<EntityId>, IRegister, IEnable
{
    public EntityId Key { get; init; } = Guid.CreateVersion7();
    public DateTimeOffset Register { get; init; } = DateTimeOffset.UtcNow;
    public bool IsEnable { get; set; } = true;
}