using Forest.Minimal.Common.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi(op => op.AddDocumentTransformer<BearerTransformer>());

builder.AddServiceDefaults()
    .AddValidationConfiguration()
    .AddBearer()
    .AddObjectStorage()
    .AddContext()
    .AddAuth();

builder.Services.AddMiddlewares()
    .AddEndpoints()
    .AddHandlers()
    .AddHash()
    .AddValidators();

var app = builder.Build();
_ = app.MigrateAsync();
_ = app.LoadBucketAsync();
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