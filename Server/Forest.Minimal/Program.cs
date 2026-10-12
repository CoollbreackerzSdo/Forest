using Forest.Minimal.Common.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi(op => op.AddDocumentTransformer<BearerTransformer>());

builder.AddServiceDefaults()
    .AddValidationConfiguration()
    .AddBearer()
    .AddObjectStorage()
    .AddContext();

builder.Services.AddMiddlewares()
    .AddEndpoints()
    .AddHandlers()
    .AddHash()
    .AddValidators();

var app = builder.Build();
_ = app.MigrateAsync();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/", op => op.WithTheme(ScalarTheme.BluePlanet));
}
app.UseHttpsRedirection();
app.MapMiddleware();
app.MapGet("health/check", () => TypedResults.Ok());
app.MapEndpoints();
app.Run();