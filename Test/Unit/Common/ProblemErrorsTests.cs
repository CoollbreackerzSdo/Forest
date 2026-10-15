using System.Text.Json;
using EntityFramework.Exceptions.Common;
using Forest.Minimal.Account.Models;
using Forest.Minimal.Common.Middlewares;
using Forest.Test.Support;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Forest.Test.Unit.Common;

public sealed class ProblemErrorsTests : IDisposable
{
    private readonly ForestContextScope _scope = ForestContextScope.Create();

    public ProblemErrorsTests() => TestData.SeedUser(_scope.Context, "usuario", "usuario@forest.dev");

    /// <summary>Marca <c>IsEnable</c> como modificada y devuelve la entrada del ChangeTracker.</summary>
    private EntityEntry Entry()
    {
        var entry = _scope.Context.ChangeTracker.Entries<UserModel>().Single();
        entry.Entity.IsEnable = false;
        _scope.Context.ChangeTracker.DetectChanges();
        return entry;
    }

    /// <summary>Entrada del ChangeTracker sin ninguna propiedad modificada.</summary>
    private EntityEntry UntouchedEntry() => _scope.Context.ChangeTracker.Entries<UserModel>().Single();

    private Exception Unique() => new UniqueConstraintException("duplicado", [Entry()]);
    private Exception Reference() => new ReferenceConstraintException("referencia", [Entry()]);
    private Exception CannotInsertNull() => new CannotInsertNullException("columna", [Entry()]);
    private Exception MaxLength() => new MaxLengthExceededException("columna", [Entry()]);
    private Exception NumericOverflow() => new NumericOverflowException("columna", [Entry()]);

    [Theory]
    [InlineData(typeof(UniqueConstraintException), StatusCodes.Status409Conflict, "Conflicto de datos")]
    [InlineData(typeof(ReferenceConstraintException), StatusCodes.Status409Conflict, "Conflicto de referencias")]
    [InlineData(typeof(CannotInsertNullException), StatusCodes.Status400BadRequest, "Dato obligatorio")]
    [InlineData(typeof(MaxLengthExceededException), StatusCodes.Status400BadRequest, "Longitud excedida")]
    [InlineData(typeof(NumericOverflowException), StatusCodes.Status400BadRequest, "Valor numérico inválido")]
    [InlineData(typeof(DbUpdateConcurrencyException), StatusCodes.Status409Conflict, "Conflicto de concurrencia")]
    [InlineData(typeof(BadHttpRequestException), StatusCodes.Status400BadRequest, "Cuerpo de la solicitud sin definir")]
    [InlineData(typeof(InvalidOperationException), StatusCodes.Status418ImATeapot, "Cafetera en problemas")]
    public void From_DeberiaTraducirCadaExcepcion(Type exceptionType, int expectedStatus, string expectedTitle)
    {
        var problem = ProblemErrors.From(Create(exceptionType));

        Assert.Equal(expectedStatus, problem.Status);
        Assert.Equal(expectedTitle, problem.Title);
        Assert.NotEmpty(problem.Detail);
    }

    [Fact]
    public void From_DeberiaCaerEnTeapotParaExcepcionesDesconocidas()
    {
        Assert.Equal(StatusCodes.Status418ImATeapot, ProblemErrors.From(new NotSupportedException()).Status);
    }

    [Fact]
    public void From_DeberiaConservarElDetailDeCadaError()
    {
        Assert.Equal("Ya existe un registro con esos datos.", ProblemErrors.From(Unique()).Detail);
        Assert.Equal("Ocurrió un una sobrecarga inesperada al procesar tu cafe en la solicitud.", ProblemErrors.From(new NotSupportedException("motivo")).Detail);
    }

    [Theory]
    [InlineData("unique")]
    [InlineData("reference")]
    [InlineData("insert-null")]
    [InlineData("max-length")]
    [InlineData("numeric-overflow")]
    public void From_DeberiaDevolverLasPropiedadesQueProvocaronElError(string kind)
    {
        var problem = ProblemErrors.From(CreateByKind(kind));

        Assert.Equal(["IsEnable"], problem.Fields);
    }

    [Fact]
    public void From_NoDeberiaDevolverPropiedadesSiNingunaFueModificada()
    {
        var problem = ProblemErrors.From(new MaxLengthExceededException("error", [UntouchedEntry()]));

        Assert.Empty(problem.Fields!);
    }

    [Fact]
    public void From_NoDeberiaDevolverPropiedadesEnExcepcionesSinEntradas()
    {
        Assert.Null(ProblemErrors.From(new DbUpdateConcurrencyException("no existe")).Fields);
        Assert.Null(ProblemErrors.From(new BadHttpRequestException("formato")).Fields);
    }

    private Exception Create(Type exceptionType) => exceptionType switch
    {
        _ when exceptionType == typeof(UniqueConstraintException) => Unique(),
        _ when exceptionType == typeof(ReferenceConstraintException) => Reference(),
        _ when exceptionType == typeof(CannotInsertNullException) => CannotInsertNull(),
        _ when exceptionType == typeof(MaxLengthExceededException) => MaxLength(),
        _ when exceptionType == typeof(NumericOverflowException) => NumericOverflow(),
        _ when exceptionType == typeof(DbUpdateConcurrencyException) => new DbUpdateConcurrencyException("no existe"),
        _ when exceptionType == typeof(BadHttpRequestException) => new BadHttpRequestException("formato"),
        _ => (Exception)Activator.CreateInstance(exceptionType)!
    };

    private Exception CreateByKind(string kind) => kind switch
    {
        "unique" => Unique(),
        "reference" => Reference(),
        "insert-null" => CannotInsertNull(),
        "max-length" => MaxLength(),
        _ => NumericOverflow()
    };

    public void Dispose()
    {
        _scope.Dispose();
        GC.SuppressFinalize(this);
    }
}

public sealed class ProblemTests
{
    [Fact]
    public async Task ExecuteAsync_DeberiaEscribirElStatusElContentTypeYElCuerpo()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await new Problem(StatusCodes.Status409Conflict, "Conflicto de datos", "Ya existe un registro con esos datos.", new[] { "email" }).ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(409, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Conflicto de datos", document.RootElement.GetProperty("title").GetString());
        Assert.Equal("email", document.RootElement.GetProperty("fields")[0].GetString());
    }

    [Fact]
    public async Task ExecuteAsync_NoDeberiaSerializarLasPropiedadesNulas()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await new Problem(StatusCodes.Status404NotFound, "Imagen no encontrada", "No existe una imagen con ese identificador.").ExecuteAsync(context);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.False(document.RootElement.TryGetProperty("fields", out _));
        Assert.False(document.RootElement.TryGetProperty("errors", out _));
        Assert.False(document.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public void WithTrace_NoDebeModificarLaInstanciaOriginal()
    {
        var original = new Problem(StatusCodes.Status404NotFound, "Imagen no encontrada", "No existe una imagen con ese identificador.");

        var traced = original.WithTrace("abc123");

        Assert.Equal("abc123", traced.TraceId);
        Assert.Null(original.TraceId);
        Assert.Equal(original, traced with { TraceId = null });
    }

    [Fact]
    public void ElRecordDeberiaSerInmutableYTenerIgualdadPorValor()
    {
        var a = new Problem(404, "Imagen no encontrada", "detalle");
        var b = new Problem(404, "Imagen no encontrada", "detalle");

        Assert.Equal(a, b);
        Assert.NotEqual(a, b with { Status = 500 });
    }

    [Fact]
    public void LosErroresDeValidacionDeberianViajarEnElCampoErrors()
    {
        var errors = new Dictionary<string, string[]> { ["Password"] = ["muy corta"] };

        var problem = new Problem(StatusCodes.Status400BadRequest, "Solicitud inválida", "Uno o más campos no pasaron la validación.", Errors: errors);

        Assert.Equal(errors, problem.Errors);
        Assert.Null(problem.Fields);
    }
}