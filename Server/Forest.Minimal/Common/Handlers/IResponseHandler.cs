namespace Forest.Minimal.Common.Handlers;

public interface IResponseHandler<TResponse>
{
    Task<Result<TResponse>> HandlerAsync(CancellationToken token);
}
