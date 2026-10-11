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
                HashKey = Environment.GetEnvironmentVariable("BEARER_KEY") ?? throw new ArgumentNullException("Variable de entorno {BEARER_KEY} no proporcionada"),
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
            builder.AddNpgsqlDbContext<Context>("forest-database", configureDbContextOptions: op => op.UseExceptionProcessor());
            return builder;
        }
    }
    extension(IServiceCollection services)
    {
        public IServiceCollection AddValidators() => services.AddSingleton<IValidator<SignUpCommand>, SignUpValidator>();
        public IServiceCollection AddHandlers() => services.AddTransient<IHandler<SignUpCommand, BearerTransport>, SignUpHandler>();
        public IServiceCollection AddHash() => services.AddSingleton<IPasswordHasher<SignUpCommand>, PasswordHasher<SignUpCommand>>();
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
        public IEndpointRouteBuilder MapEndpoints()
        {
            foreach (var item in app.Services.GetRequiredService<IEnumerable<IEndpoint>>()) item.Map(app);
            return app;
        }
        public IApplicationBuilder MapMiddleware() => app.UseMiddleware<ErrorMiddleware>();
    }
}