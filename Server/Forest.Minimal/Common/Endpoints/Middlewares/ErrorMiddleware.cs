using EntityFramework.Exceptions.Common;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Forest.Minimal.Common.Endpoints.Middlewares;

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

            var problem = Map(exception);
            if (problem.Status >= StatusCodes.Status500InternalServerError)
            {
                logger.LogError(exception, "Error no controlado en la solicitud {TraceId}.", context.TraceIdentifier);
            }
            else
            {
                logger.LogWarning(exception, "Error controlado en la solicitud {TraceId}.", context.TraceIdentifier);
            }

            context.Response.StatusCode = (int)problem?.Status!;
            context.Response.ContentType = "application/problem+json";
            problem.Extensions["traceId"] = context.TraceIdentifier;

            await context.Response.WriteAsJsonAsync(problem);
        }
    }

    private static ProblemDetails Map(Exception exception)
    {
        return exception switch
        {
            UniqueConstraintException => new()
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflicto de datos",
                Detail = "Ya existe un registro con esos datos."
            },
            ReferenceConstraintException => new()
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflicto de referencias",
                Detail = "La operación entra en conflicto con registros relacionados."
            },
            CannotInsertNullException => new()
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Dato obligatorio",
                Detail = "Falta un valor obligatorio para completar la operación."
            },
            MaxLengthExceededException => new()
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Longitud excedida",
                Detail = "Uno o más valores superan la longitud permitida."
            },
            NumericOverflowException => new()
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Valor numérico inválido",
                Detail = "Uno o más valores numéricos están fuera del rango permitido."
            },
            DbUpdateConcurrencyException => new()
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflicto de concurrencia",
                Detail = "El registro cambió desde que se consultó. Actualiza los datos e inténtalo de nuevo.",
            },
            BadHttpRequestException => new()
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Cuerpo de la solicitud sin definir",
                Detail = "El formato del cuerpo de la solicitud no esta definido",
            },
            _ => new()
            {
                Status = StatusCodes.Status418ImATeapot,
                Title = "Cafetera en problemas",
                Detail = "Ocurrió un una sobrecarga inesperada al procesar tu cafe en la solicitud."
            }
        };
    }
}