namespace Forest.Minimal.Common.Middlewares;

public sealed class ErrorMiddleware(ILogger<ErrorMiddleware> logger) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug("La solicitud {TraceId} fue cancelada por el cliente.", context.TraceIdentifier);
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
            {
                logger.LogError(exception, "No se puede generar una respuesta de error porque la respuesta ya comenzó.");
                throw;
            }

            var problem = ProblemErrors.From(exception);
            if (problem.Status >= StatusCodes.Status500InternalServerError)
            {
                logger.LogError(exception, "Error no controlado en la solicitud {TraceId}.", context.TraceIdentifier);
            }
            else
            {
                logger.LogWarning(exception, "Error controlado en la solicitud {TraceId}.", context.TraceIdentifier);
            }

            await problem.WithTrace(context.TraceIdentifier).ExecuteAsync(context);
        }
    }
}