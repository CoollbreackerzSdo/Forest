using EntityFramework.Exceptions.PostgreSQL;
using FluentValidation;
using Forest.Minimal.Account.Handlers;
using Forest.Minimal.Account.Services.Bearer;
using Forest.Minimal.Account.Services.Bearer.Generator;
using Forest.Minimal.Account.Services.Bearer.Models;
using Forest.Minimal.Account.Services.Validator;
using Forest.Minimal.Common.Endpoints.Abstractions;
using Forest.Minimal.Common.Endpoints.Middlewares;
using Forest.Minimal.Common.Handlers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RustfsClient;
using RustfsClient.Options;

namespace Forest.Minimal;

public static partial class Extensions
{
    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddBearer()
        {
            builder.Configuration.AddEnvironmentVariables("BEARER_KEY");
            builder.Services.AddSingleton(new BearerConfiguration()
            {
                HashKey = Environment.GetEnvironmentVariable("BEARER_KEY") ?? string.Empty,
                Audience = builder.Configuration.GetValue<string>("Parameters:BEARER_AUDIENCE") ?? string.Empty,
                Issuer = builder.Configuration.GetValue<string>("Parameters:BEARER_ISSUE") ?? string.Empty,
                Exp = builder.Configuration.GetValue<int>("Parameters:BEARER_EXP_DAYS"),
                RefreshExp = builder.Configuration.GetValue<int>("Parameters:BEARER_REFRESH_EXP_DAYS"),
            });
            builder.Services.AddSingleton<IBearerGenerator, BearerGenerator>();
            return builder;
        }
        public IHostApplicationBuilder AddContext()
        {
            builder.AddNpgsqlDbContext<ForestContext>("forest-database", configureDbContextOptions: op => op.UseExceptionProcessor());
            return builder;
        }
        public IHostApplicationBuilder AddObjectStorage()
        {
            builder.Services.AddRustfsClient(new RustfsClientOptions
            {
                ServiceUrl = builder.Configuration.GetValue<string>("RUSTFS_SERVICE_URL") ?? "http://localhost:5613",
                AccessKey = builder.Configuration.GetValue<string>("RUSTFS_ACCESS_KEY") ?? string.Empty,
                SecretKey = builder.Configuration.GetValue<string>("RUSTFS_SECRET_KEY") ?? string.Empty,
                Region = builder.Configuration.GetValue<string>("RUSTFS_REGION", "us-east-1"),
            });
            return builder;
        }
    }
    extension(IServiceCollection services)
    {
        public IServiceCollection AddValidators() => services
            .AddSingleton<IValidator<SignUpCommand>, SignUpValidator>()
            .AddSingleton<IValidator<SignInCommand>, SignInValidator>();
        public IServiceCollection AddHandlers() => services
            .AddTransient<IHandler<SignUpCommand, BearerTransport>, SignUpHandler>()
            .AddTransient<IHandler<SignInCommand, BearerTransport>, SignInHandler>()
            .AddTransient<IHandler<ImageQuery, ImageResult>, ImageHandler>();
        public IServiceCollection AddHash() => services
            .AddSingleton<IPasswordHasher<SignUpCommand>, PasswordHasher<SignUpCommand>>()
            .AddSingleton<IPasswordHasher<SignInCommand>, PasswordHasher<SignInCommand>>();
        public IServiceCollection AddEndpoints()
        {
            services.TryAddEnumerable(typeof(Program).Assembly.DefinedTypes
            .Where(x => !x.IsAbstract && !x.IsInterface && x.IsAssignableTo(typeof(IEndpoint)))
            .Select(x => ServiceDescriptor.Transient(typeof(IEndpoint), x)));
            return services;
        }
        public IServiceCollection AddMiddlewares() => services.AddTransient<ErrorMiddleware>();
    }
    extension(WebApplication app)
    {
        public async Task MigrateAsync() => await app.Services.CreateScope().ServiceProvider.GetRequiredService<ForestContext>().Database.MigrateAsync();
        public IEndpointRouteBuilder MapEndpoints()
        {
            foreach (var item in app.Services.GetRequiredService<IEnumerable<IEndpoint>>()) item.Map(app);
            return app;
        }
        public IApplicationBuilder MapMiddleware() => app.UseMiddleware<ErrorMiddleware>();
    }
}