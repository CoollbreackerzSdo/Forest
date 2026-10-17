using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Forest.Minimal.Common.Middlewares;

public sealed record Problem(
    int Status,
    string Title,
    string Detail,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Fields = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IDictionary<string, string[]>? Errors = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TraceId = null) : Microsoft.AspNetCore.Http.IResult
{
    public const string ContentType = "application/problem+json; charset=utf-8";
    private static readonly JsonSerializerOptions Serializer = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    public Problem WithTrace(string traceId) => this with { TraceId = traceId };
    public async Task ExecuteAsync(HttpContext context)
    {
        context.Response.StatusCode = Status;
        context.Response.ContentType = ContentType;
        await context.Response.WriteAsync(JsonSerializer.Serialize(this, Serializer), Encoding.UTF8, context.RequestAborted);
    }
}