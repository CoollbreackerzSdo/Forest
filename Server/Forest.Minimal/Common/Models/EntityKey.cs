namespace Forest.Minimal.Common.Models;

public record struct EntityKey<T>(T Value) : IComparable<EntityKey<T>>
    where T : notnull, IComparable<T>
{
    public override readonly string? ToString() => Value.ToString();
    public readonly int CompareTo(EntityKey<T> other) => Value.CompareTo(other.Value);
    public readonly T Value = Value;
    public static implicit operator T(EntityKey<T> value) => value.Value;
    public static implicit operator EntityKey<T>(T value) => new(value);
}
