namespace Forest.Minimal.Common.Handlers;

public interface IHandler<TRequest, TResponse>
{
    Task<Result<TResponse>> HandlerAsync(TRequest request, CancellationToken token);
}