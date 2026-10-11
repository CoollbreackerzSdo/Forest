namespace Forest.Minimal.Common.Handlers;

public interface IRequestHandler<TRequest>
{
    Task<Result> HandlerAsync(TRequest request, CancellationToken token);
}