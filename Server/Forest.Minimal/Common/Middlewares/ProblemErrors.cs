using EntityFramework.Exceptions.Common;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Forest.Minimal.Common.Middlewares;

/// <summary>
/// Traduce las excepciones no controladas al <see cref="Problem"/> que se devuelve al cliente.
/// Los errores que generan los endpoints y los filtros se construyen con el <see cref="Problem"/>
/// directamente, sin pasar por aqui.
/// </summary>
public static class ProblemErrors
{
    public static Problem From(Exception exception) => exception switch
    {
        UniqueConstraintException x => new Problem(
            StatusCodes.Status409Conflict,
            "Conflicto de datos",
            "Ya existe un registro con esos datos.",
            x.ConstraintProperties is { Count: > 0 } properties ? properties : Names(x.Entries)),

        ReferenceConstraintException x => new Problem(
            StatusCodes.Status409Conflict,
            "Conflicto de referencias",
            "La operación entra en conflicto con registros relacionados.",
            x.ConstraintProperties is { Count: > 0 } properties ? properties : Names(x.Entries)),

        CannotInsertNullException x => new Problem(
            StatusCodes.Status400BadRequest,
            "Dato obligatorio",
            "Falta un valor obligatorio para completar la operación.",
            Names(x.Entries)),

        MaxLengthExceededException x => new Problem(
            StatusCodes.Status400BadRequest,
            "Longitud excedida",
            "Uno o más valores superan la longitud permitida.",
            Names(x.Entries)),

        NumericOverflowException x => new Problem(
            StatusCodes.Status400BadRequest,
            "Valor numérico inválido",
            "Uno o más valores numéricos están fuera del rango permitido.",
            Names(x.Entries)),

        DbUpdateConcurrencyException => new Problem(
            StatusCodes.Status409Conflict,
            "Conflicto de concurrencia",
            "El registro cambió desde que se consultó. Actualiza los datos e inténtalo de nuevo."),

        BadHttpRequestException => new Problem(
            StatusCodes.Status400BadRequest,
            "Cuerpo de la solicitud sin definir",
            "El formato del cuerpo de la solicitud no esta definido"),

        _ => new Problem(
            StatusCodes.Status418ImATeapot,
            "Cafetera en problemas",
            "Ocurrió un una sobrecarga inesperada al procesar tu cafe en la solicitud.")
    };

    /// <summary>
    /// Nombres de las propiedades modificadas que provocaron el error, leidos del
    /// ChangeTracker que las excepciones de <c>EntityFramework.Exceptions</c> arrastran.
    /// </summary>
    private static IReadOnlyList<string>? Names(IReadOnlyList<EntityEntry> entries) => [
        .. entries
        .SelectMany(x => x.Properties)
        .Where(x => x.IsModified)
        .Select(x => x.Metadata.Name)
        .Distinct()
    ];
}