using Forest.Minimal.Common.Services.Validation;
using Microsoft.Extensions.Options;

namespace Forest.Test.Support;

/// <summary>
/// <see cref="IOptionsMonitor{T}"/> fijo para injectar la configuracion de validacion en los validators.
/// </summary>
public sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
{
    public T CurrentValue { get; } = value;
    public T Get(string? name) => CurrentValue;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}