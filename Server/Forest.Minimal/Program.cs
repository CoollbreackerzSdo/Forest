using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.AddServiceDefaults()
    .AddBearer()
    .AddContext();

builder.Services.AddMiddlewares()
    .AddEndpoints()
    .AddHandlers()
    .AddHash();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/", op => op.WithTheme(ScalarTheme.BluePlanet));
}
app.UseHttpsRedirection();
app.MapMiddleware();
app.MapEndpoints();
app.Run();