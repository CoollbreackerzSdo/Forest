using System.Text.Json;
using EntityFramework.Exceptions.Common;
using EntityFramework.Exceptions.PostgreSQL;
using Forest.Minimal.Common.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Forest.Test.Unit.Common;

public sealed class ErrorMiddlewareTests
{
    private static readonly ErrorMiddleware Middleware = new(NullLogger<ErrorMiddleware>.Instance);

    [Theory]
    [InlineData(typeof(UniqueConstraintException), StatusCodes.Status409Conflict, "Conflicto de datos")]
    [InlineData(typeof(ReferenceConstraintException), StatusCodes.Status409Conflict, "Conflicto de referencias")]
    [InlineData(typeof(CannotInsertNullException), StatusCodes.Status400BadRequest, "Dato obligatorio")]
    [InlineData(typeof(MaxLengthExceededException), StatusCodes.Status400BadRequest, "Longitud excedida")]
    [InlineData(typeof(NumericOverflowException), StatusCodes.Status400BadRequest, "Valor numérico inválido")]
    [InlineData(typeof(DbUpdateConcurrencyException), StatusCodes.Status409Conflict, "Conflicto de concurrencia")]
    [InlineData(typeof(BadHttpRequestException), StatusCodes.Status400BadRequest, "Cuerpo de la solicitud sin definir")]
    [InlineData(typeof(InvalidOperationException), StatusCodes.Status418ImATeapot, "Cafetera en problemas")]
    public async Task InvokeAsync_DeberiaTraducirLaExcepcionAProblem(Type exceptionType, int expectedStatus, string expectedTitle)
    {
        var (context, body) = await InvokeAsync(Create(exceptionType));

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Equal(Problem.ContentType, context.Response.ContentType);
        Assert.Equal(expectedTitle, body.GetProperty("title").GetString());
        Assert.Equal(expectedStatus, body.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task InvokeAsync_DeberiaIncluirElTraceIdEnElCuerpo()
    {
        var (context, body) = await InvokeAsync(new InvalidOperationException("boom"));

        Assert.Equal(context.TraceIdentifier, body.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task InvokeAsync_DeberiaDevolverTeapotParaExcepcionesDesconocidas()
    {
        var (_, body) = await InvokeAsync(new Exception("inesperada"));

        Assert.Equal(StatusCodes.Status418ImATeapot, body.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task InvokeAsync_NoDebePropagarExcepcionesControladas()
    {
        await InvokeAsync(Create(typeof(UniqueConstraintException)));

        // Si llegamos aqui la excepcion fue absorbida por el middleware.
        Assert.True(true);
    }

    [Fact]
    public async Task InvokeAsync_DeberiaIgnorarCancelacionesDelCliente()
    {
        using var abort = new CancellationTokenSource();
        await abort.CancelAsync();
        var context = new DefaultHttpContext { RequestAborted = abort.Token };
        var body = new MemoryStream();
        context.Response.Body = body;

        await Middleware.InvokeAsync(context, _ => throw new OperationCanceledException(abort.Token));

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(0, body.Length);
    }

    [Fact]
    public async Task InvokeAsync_DeberiaPropagarLaExcepcionSiLaRespuestaYaEmpezo()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        // Forzar HasStarted: DefaultHttpContext no marca la respuesta como iniciada al escribir en el cuerpo.
        context.Features.Set<IHttpResponseFeature>(new ResponseFeature(context.Features.Get<IHttpResponseFeature>()!) { HasStarted = true });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Middleware.InvokeAsync(context, _ => throw new InvalidOperationException("boom")));
    }

    [Fact]
    public async Task InvokeAsync_DeberiaDejarPasarLasRespuestasCorrectas()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await Middleware.InvokeAsync(context, async ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status201Created;
            await ctx.Response.WriteAsync("creado");
        });

        Assert.Equal(StatusCodes.Status201Created, context.Response.StatusCode);
    }

    private static Exception Create(Type exceptionType) => exceptionType switch
    {
        _ when exceptionType == typeof(UniqueConstraintException) => new UniqueConstraintException("duplicado"),
        _ when exceptionType == typeof(ReferenceConstraintException) => new ReferenceConstraintException("referencia"),
        _ when exceptionType == typeof(CannotInsertNullException) => new CannotInsertNullException("columna"),
        _ when exceptionType == typeof(MaxLengthExceededException) => new MaxLengthExceededException("columna"),
        _ when exceptionType == typeof(NumericOverflowException) => new NumericOverflowException("columna"),
        _ when exceptionType == typeof(DbUpdateConcurrencyException) => new DbUpdateConcurrencyException("no existe"),
        _ when exceptionType == typeof(BadHttpRequestException) => new BadHttpRequestException("formato"),
        _ => (Exception)Activator.CreateInstance(exceptionType)!
    };

    private static async Task<(DefaultHttpContext Context, JsonElement Body)> InvokeAsync(Exception exception)
    {
        var context = new DefaultHttpContext();
        var buffer = new MemoryStream();
        context.Response.Body = buffer;
        context.Response.ContentType = null;

        await Middleware.InvokeAsync(context, _ => throw exception);

        buffer.Position = 0;
        using var document = await JsonDocument.ParseAsync(buffer, new JsonDocumentOptions());
        return (context, document.RootElement.Clone());
    }

    /// <summary>
    /// <see cref="IHttpResponseFeature"/> que permite forzar <c>HasStarted</c>, algo que
    /// <see cref="DefaultHttpContext"/> no hace por si mismo.
    /// </summary>
    private sealed class ResponseFeature(IHttpResponseFeature inner) : IHttpResponseFeature
    {
#pragma warning disable CS0618 // IHttpResponseFeature.Body sigue siendo parte del contrato; DefaultHttpContext lo usa.
        public Stream Body { get => _body ??= inner.Body; set => _body = value; }
#pragma warning restore CS0618
        private Stream? _body;
        public bool HasStarted { get; set; }
        public int StatusCode { get => inner.StatusCode; set => inner.StatusCode = value; }
        public string? ReasonPhrase { get => inner.ReasonPhrase; set => inner.ReasonPhrase = value; }
        public IHeaderDictionary Headers { get => inner.Headers; set => inner.Headers = value; }
        public void OnStarting(Func<object, Task> callback, object state) => inner.OnStarting(callback, state);
        public void OnCompleted(Func<object, Task> callback, object state) => inner.OnCompleted(callback, state);
    }
}