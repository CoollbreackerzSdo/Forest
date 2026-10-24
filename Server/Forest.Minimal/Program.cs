using Forest.Minimal.Common.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables("REDIS_CONNECTION");

builder.Services.AddOpenApi(op => op.AddDocumentTransformer<BearerTransformer>());

builder.AddLogs()
    .AddServiceDefaults()
    .AddValidationConfiguration()
    .AddBearer()
    .AddObjectStorage()
    .AddImageCleaner()
    .AddContext()
    .AddAuth()
    .AddRedisDistributedCache("forest-cache", op => op.ConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION"));

builder.Services.AddMiddlewares()
    .AddEndpoints()
    .AddChannels()
    .AddHandlers()
    .AddHash()
    .AddValidators();

var app = builder.Build();
await app.MigrateAsync();
await app.LoadBucketAsync();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/", op => op.WithTheme(ScalarTheme.BluePlanet));
}
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapMiddleware();
app.MapGet("health/check", () => TypedResults.Ok());
app.MapEndpoints();
app.Run();