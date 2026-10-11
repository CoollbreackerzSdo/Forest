using EntityFramework.Exceptions.Common;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

            var error = Map(exception);
            if (error.StatusCode >= StatusCodes.Status500InternalServerError)
            {
                logger.LogError(exception, "Error no controlado en la solicitud {TraceId}.", context.TraceIdentifier);
            }
            else
            {
                logger.LogWarning(exception, "Error controlado en la solicitud {TraceId}.", context.TraceIdentifier);
            }

            context.Response.StatusCode = error.StatusCode;
            context.Response.ContentType = "application/problem+json";

            var problem = new ProblemDetails
            {
                Status = error.StatusCode,
                Title = error.Title,
                Detail = error.Detail,
                Instance = context.Request.Path
            };

            problem.Extensions["traceId"] = context.TraceIdentifier;

            if (error.Errors is not null)
            {
                problem.Extensions["errors"] = error.Errors;
            }

            await context.Response.WriteAsJsonAsync(problem);
        }
    }

    private static MappedError Map(Exception exception)
    {
        return exception switch
        {
            ValidationException validationException => MapValidation(validationException),
            UniqueConstraintException => new(
                StatusCodes.Status409Conflict,
                "Conflicto de datos",
                "Ya existe un registro con esos datos."),
            ReferenceConstraintException => new(
                StatusCodes.Status409Conflict,
                "Conflicto de referencias",
                "La operación entra en conflicto con registros relacionados."),
            CannotInsertNullException => new(
                StatusCodes.Status400BadRequest,
                "Dato obligatorio",
                "Falta un valor obligatorio para completar la operación."),
            MaxLengthExceededException => new(
                StatusCodes.Status400BadRequest,
                "Longitud excedida",
                "Uno o más valores superan la longitud permitida."),
            NumericOverflowException => new(
                StatusCodes.Status400BadRequest,
                "Valor numérico inválido",
                Detail: "Uno o más valores numéricos están fuera del rango permitido."),
            DbUpdateConcurrencyException => new(
                StatusCodes.Status409Conflict,
                "Conflicto de concurrencia",
                "El registro cambió desde que se consultó. Actualiza los datos e inténtalo de nuevo."),
            _ => new(
                StatusCodes.Status418ImATeapot,
                "Cafetera en problemas",
                "Ocurrió un una sobrecarga inesperada al procesar tu cafe en la solicitud.")
        };
    }

    private static MappedError MapValidation(ValidationException exception)
    {
        var errors = exception.Errors.GroupBy(error => error.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).Distinct().ToArray());

        return new MappedError(StatusCodes.Status400BadRequest, "Solicitud inválida", "Uno o más campos no superaron la validación.", errors);
    }

    private sealed record MappedError(int StatusCode, string Title, string Detail, IReadOnlyDictionary<string, string[]>? Errors = null);
}
