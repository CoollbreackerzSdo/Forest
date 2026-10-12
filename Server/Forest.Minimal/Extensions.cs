using EntityFramework.Exceptions.PostgreSQL;
using FluentValidation;
using Forest.Communication.Models.Storage;
using Forest.Minimal.Account.Handlers;
using Forest.Minimal.Account.Services.Bearer;
using Forest.Minimal.Account.Services.Bearer.Generator;
using Forest.Minimal.Account.Services.Bearer.Models;
using Forest.Minimal.Account.Services.Validator;
using Forest.Minimal.Common.Endpoints.Abstractions;
using Forest.Minimal.Common.Endpoints.Middlewares;
using Forest.Minimal.Common.Handlers;
using Forest.Minimal.Common.Services.Validation;
using Forest.Minimal.Storage.Handlers;
using Forest.Minimal.Storage.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RustfsClient;
using RustfsClient.Abstractions;
using RustfsClient.Options;

namespace Forest.Minimal;

public static partial class Extensions
{
    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddValidationConfiguration()
        {
            builder.Services.Configure<ValidationConfiguration>(builder.Configuration.GetSection("ValidationConfiguration"));
            return builder;
        }
        public IHostApplicationBuilder AddBearer()
        {
            builder.Configuration.AddEnvironmentVariables("BEARER_KEY");
            builder.Services.AddSingleton(new BearerConfiguration()
            {
                HashKey = Environment.GetEnvironmentVariable("BEARER_KEY") ?? string.Empty,
                Audience = builder.Configuration.GetValue<string>("Parameters:BEARER_AUDIENCE")!,
                Issuer = builder.Configuration.GetValue<string>("Parameters:BEARER_ISSUE")!,
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
            builder.Configuration.AddEnvironmentVariables("RUSTFS_SERVICE_URL");
            builder.Configuration.AddEnvironmentVariables("RUSTFS_ACCESS_KEY");
            builder.Configuration.AddEnvironmentVariables("RUSTFS_SECRET_KEY");
            builder.Configuration.AddEnvironmentVariables("RUSTFS_REGION");

            builder.Services.AddRustfsClient(new RustfsClientOptions
            {
                ServiceUrl = Environment.GetEnvironmentVariable("RUSTFS_SERVICE_URL")!,
                AccessKey = Environment.GetEnvironmentVariable("RUSTFS_ACCESS_KEY")!,
                SecretKey = Environment.GetEnvironmentVariable("RUSTFS_SECRET_KEY")!,
                Region = Environment.GetEnvironmentVariable("RUSTFS_REGION")!,
            });
            return builder;
        }
    }
    extension(IServiceCollection services)
    {
        public IServiceCollection AddValidators() => services
            .AddSingleton<IValidator<SignUpCommand>, SignUpValidator>()
            .AddSingleton<IValidator<SignInRequest>, SignInValidator>();
        public IServiceCollection AddHandlers() => services
            .AddTransient<IHandler<SignUpCommand, BearerTransport>, SignUpHandler>()
            .AddTransient<IHandler<SignInRequest, BearerTransport>, SignInHandler>()
            .AddTransient<IHandler<ImageQuery, ImageResult>, ImageHandler>();
        public IServiceCollection AddHash() => services
            .AddSingleton<IPasswordHasher<SignUpCommand>, PasswordHasher<SignUpCommand>>()
            .AddSingleton<IPasswordHasher<SignInRequest>, PasswordHasher<SignInRequest>>();
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
    extension(IRustfsObjectStorageClient client)
    {
        public async Task<string> UploadAsync(IFormFile file, CancellationToken token)
        {
            await client.EnsureBucketAsync(StorageHelper.BucketName, token);

            await using var stream = new MemoryStream();
            await using (var source = file.OpenReadStream())
            {
                await source.CopyToAsync(stream, token);
            }
            stream.Position = 0;

            var key = $"{Guid.CreateVersion7()}{Path.GetExtension(file.FileName)}";
            await client.UploadAsync(StorageHelper.BucketName, key, stream, file.ContentType, token);
            return key;
        }
        public async Task DeleteAsync(string id, CancellationToken token) => await client.DeleteAsync(StorageHelper.BucketName, id, token);
    }
}