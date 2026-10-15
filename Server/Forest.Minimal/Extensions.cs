using System.Text;
using System.Threading.Channels;
using EntityFramework.Exceptions.PostgreSQL;
using FluentValidation;
using Forest.Communication.Models.Storage;
using Forest.Minimal.Account.Handlers;
using Forest.Minimal.Account.Models;
using Forest.Minimal.Account.Services.Bearer;
using Forest.Minimal.Account.Services.Bearer.Events;
using Forest.Minimal.Account.Services.Bearer.Generator;
using Forest.Minimal.Account.Services.Bearer.Models;
using Forest.Minimal.Account.Services.Validator;
using Forest.Minimal.Common.Endpoints.Abstractions;
using Forest.Minimal.Common.Handlers;
using Forest.Minimal.Common.Middlewares;
using Forest.Minimal.Common.Services.Validation;
using Forest.Minimal.Storage.Handlers;
using Forest.Minimal.Storage.Helpers;
using Forest.Minimal.Storage.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using RustfsClient;
using RustfsClient.Abstractions;
using RustfsClient.Options;
using Serilog;
using Serilog.Configuration;
using Serilog.Extensions.Hosting;
using Serilog.Sinks.SystemConsole.Themes;

namespace Forest.Minimal;

public static partial class Extensions
{
    /// <summary>
    /// Salida por consola: solo el nivel (coloreado por Serilog) y el mensaje, sin fecha,
    /// sin origen y sin el volcado de propiedades. Las excepciones van debajo.
    /// </summary>
    private const string OutputTemplate = "[{Level:u3}] {Message:lj}{NewLine}{Exception}";

    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddLogs()
        {
            // Se llama antes que AddServiceDefaults para poder limpiar los providers por defecto
            // (console/debug) y dejar solo el de OpenTelemetry, que se registra despues.
            builder.Logging.ClearProviders();

            // Los niveles salen de la seccion "Serilog" de appsettings. writeToProviders mantiene
            // el export de logs a OpenTelemetry.
            builder.Services.AddSerilog((_, logger) => logger
                .ReadFrom.Configuration(builder.Configuration)
                .WriteTo.Console(
                    // Ansi + applyThemeToRedirectedOutput: Aspire captura la salida por proceso,
                    // asi que sin esto los colores se pierden.
                    theme: AnsiConsoleTheme.Code,
                    applyThemeToRedirectedOutput: true,
                    outputTemplate: OutputTemplate),
                writeToProviders: true);
            return builder;
        }
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
                Audience = builder.Configuration["Parameters:BEARER_AUDIENCE"]!,
                Issuer = builder.Configuration["Parameters:BEARER_ISSUE"]!,
                Exp = builder.Configuration.GetValue<int>("Parameters:BEARER_EXP_DAYS"),
                RefreshExp = builder.Configuration.GetValue<int>("Parameters:BEARER_REFRESH_EXP_DAYS"),
            });
            builder.Services.AddSingleton<IBearerGenerator, BearerGenerator>();
            return builder;
        }
        public IHostApplicationBuilder AddAuth()
        {
            builder.Services.AddAuthentication(op =>
            {
                op.DefaultScheme = AuthHelper.DefaultScheme;
                op.DefaultAuthenticateScheme = AuthHelper.DefaultScheme;
                op.DefaultChallengeScheme = AuthHelper.DefaultScheme;
            }).AddJwtBearer(AuthHelper.DefaultScheme, op =>
            {
                op.MapInboundClaims = false;
                op.TokenValidationParameters = new()
                {
                    ValidateAudience = true,
                    ValidateIssuer = true,
                    ValidateActor = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidAudience = builder.Configuration["Parameters:BEARER_AUDIENCE"],
                    ValidIssuer = builder.Configuration["Parameters:BEARER_ISSUE"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("BEARER_KEY") ?? string.Empty))
                };
                op.Events = new()
                {
                    OnMessageReceived = MapCookieEvent.OnMessageReceived,
                    OnTokenValidated = ValidateLifeBearerEvent.OnTokenValidated
                };
            });
            builder.Services.AddAuthorizationBuilder()
                .AddDefaultPolicy(AuthHelper.DefaultPolicy, op => op.AddAuthenticationSchemes(AuthHelper.DefaultScheme).RequireAuthenticatedUser());
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
        public IHostApplicationBuilder AddImageCleaner()
        {
            builder.Services.Configure<ImageCleanerConfiguration>(builder.Configuration.GetSection(nameof(ImageCleanerConfiguration)));
            builder.Services.AddHostedService<ImageCleanerService>();
            return builder;
        }
    }
    extension(IServiceCollection services)
    {
        public IServiceCollection AddChannels() => services
            // DropOldest + TryWrite: si el canal se llena se descarta la clave mas antigua en
            // lugar de frenar al escritor. El barrido periodico rescata lo que se pierda.
            .AddKeyedSingleton<Channel<string>>(StorageHelper.CleanerChannel, (_, _) => Channel.CreateBounded<string>(new BoundedChannelOptions(1024)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
            }));
        public IServiceCollection AddValidators() => services
            .AddSingleton<IValidator<SignUpCommand>, SignUpValidator>()
            .AddSingleton<IValidator<SignInRequest>, SignInValidator>()
            .AddSingleton<IValidator<UpdateUserRequest>, UpdateUserValidator>();
        public IServiceCollection AddHandlers() => services
            .AddTransient<IHandler<SignUpCommand, BearerTransport>, SignUpHandler>()
            .AddTransient<IHandler<SignInRequest, BearerTransport>, SignInHandler>()
            .AddTransient<IHandler<ImageQuery, ImageResult>, ImageHandler>()
            .AddTransient<IRequestHandler<UpdateUserQuery>, UpdateUserHandler>()
            .AddKeyedTransient<IRequestHandler<EntityId>, SignOutHandler>("sign-out");
        public IServiceCollection AddHash() => services
            .AddSingleton<IPasswordHasher<UserModel>, PasswordHasher<UserModel>>();
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
        public async Task LoadBucketAsync(CancellationToken token = default) => await app.Services.CreateScope().ServiceProvider.GetRequiredService<IRustfsObjectStorageClient>().EnsureBucketAsync(StorageHelper.BucketName, token);
        public async Task MigrateAsync(CancellationToken token = default) => await app.Services.CreateScope().ServiceProvider.GetRequiredService<ForestContext>().Database.MigrateAsync(token);
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
            await using var source = file.OpenReadStream();
            var key = $"{Guid.CreateVersion7()}{Path.GetExtension(file.FileName)}";
            await client.UploadAsync(StorageHelper.BucketName, key, source, file.ContentType, file.Length, token);
            return key;
        }
        public async Task UpdateAsync(string key, IFormFile file, CancellationToken token)
        {
            await using var source = file.OpenReadStream();
            await client.UploadAsync(StorageHelper.BucketName, key, source, file.ContentType, file.Length, token);
        }
        public async Task DeleteAsync(string key, CancellationToken token) => await client.DeleteAsync(StorageHelper.BucketName, key, token);
    }
    extension(ValidationConfiguration configuration)
    {
        public bool ValidateFile(IFormFile file) => configuration.FileTypes.Contains(Path.GetExtension(file.FileName));
    }
}