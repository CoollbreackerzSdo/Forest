using Microsoft.Data.Sqlite;

namespace Forest.Test.Support;

/// <summary>
/// Crea un <see cref="ForestContext"/> sobre una base SQLite en memoria.
/// Se usa SQLite (y no el proveedor InMemory) porque los handlers usan
/// <c>ExecuteUpdateAsync</c> y entidades <c>Owned</c> en columna JSON, que InMemory no soporta.
/// </summary>
public sealed class ForestContextScope : IDisposable
{
    private readonly SqliteConnection _connection;

    private ForestContextScope(SqliteConnection connection, ForestContext context)
    {
        _connection = connection;
        Context = context;
    }

    public ForestContext Context { get; }

    public static ForestContextScope Create()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ForestContext>()
            .UseSqlite(connection)
            .Options;

        var context = new ForestContext(options);
        context.Database.EnsureCreated();
        return new(connection, context);
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}