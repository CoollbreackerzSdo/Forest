using System.Text.Json;
using System.Text.Json.Serialization;

namespace Forest.Minimal.Common.Middlewares;

/// <summary>
/// Respuesta de error de la API. Sustituye a <c>Microsoft.AspNetCore.Mvc.ProblemDetails</c>
/// y es a la vez el <c>IResult</c> que minimal API devuelve desde endpoints y filtros,
/// de modo que el error se construye una sola vez y no hay que convertirlo despues.
/// </summary>
public sealed record Problem(
    int Status,
    string Title,
    string Detail,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Fields = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IDictionary<string, string[]>? Errors = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TraceId = null) : Microsoft.AspNetCore.Http.IResult
{
    public const string ContentType = "application/problem+json";

    private static readonly JsonSerializerOptions Serializer = new(JsonSerializerDefaults.Web);

    /// <summary>Devuelve el error con el identificador de traza de la solicitud.</summary>
    public Problem WithTrace(string traceId) => this with { TraceId = traceId };

    public Task ExecuteAsync(HttpContext context)
    {
        context.Response.StatusCode = Status;
        context.Response.ContentType = ContentType;
        return context.Response.WriteAsync(JsonSerializer.Serialize(this, Serializer), context.RequestAborted);
    }
}