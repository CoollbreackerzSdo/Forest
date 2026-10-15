using System.Text;
using System.Text.Encodings.Web;
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
    /// <summary>
    /// El charset va explicito: sin el, los clientes que no saben que es JSON asumen
    /// ISO-8859-1 y los acentos salen como caracteres rotos.
    /// </summary>
    public const string ContentType = "application/problem+json; charset=utf-8";

    // UnsafeRelaxedJsonEscaping deja los acentos y la enye tal cual en lugar de
    // escaparlos como \u00E1, que es lo que hace el encoder por defecto.
    private static readonly JsonSerializerOptions Serializer = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Devuelve el error con el identificador de traza de la solicitud.</summary>
    public Problem WithTrace(string traceId) => this with { TraceId = traceId };

    public async Task ExecuteAsync(HttpContext context)
    {
        context.Response.StatusCode = Status;
        context.Response.ContentType = ContentType;
        await context.Response.WriteAsync(JsonSerializer.Serialize(this, Serializer), Encoding.UTF8, context.RequestAborted);
    }
}