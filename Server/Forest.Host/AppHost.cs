using Forest.Host;

var builder = DistributedApplication.CreateBuilder(args);

var bearerKey = builder.AddParameter("BearerKey", true);
var accessKey = builder.AddParameter("AccessKey", true);
var secretKey = builder.AddParameter("SecretKey", true);


var database = builder.AddPostgres("forest-database")
    .WithImageTag("18.6-alpine3.24")
    .WithVolume("forest", "/var/lib/postgres")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgAdmin(op => op.WithLifetime(ContainerLifetime.Persistent).WithHttpEndpoint(7001).WithImageTag("9.18"), "forest-pg")
    .WithEndpoint(7000);

var cache = builder.AddRedis("forest-cache")
    .WithVolume("forest", "/data")
    .WithImageTag("latest")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithEndpoint(7002);

var rustfs = builder.AddRustfs("forest-s3", accessKey, secretKey, 7003, 7004)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithVolume("forest", "/data");

var api = builder.AddProject<Projects.Forest_Minimal>("forest-server")
    .WithEnvironment("BEARER_KEY", bearerKey)
    .WithEnvironment("RUSTFS_ACCESS_KEY", accessKey)
    .WithEnvironment("RUSTFS_SECRET_KEY", secretKey)
    .WithEnvironment("RUSTFS_SERVICE_URL", "http://localhost:7003")
    .WithEnvironment("RUSTFS_REGION", "us-east-1")
    .WithReference(cache)
    .WithReference(rustfs)
    .WithReference(database)
    .WaitFor(cache)
    .WaitFor(database)
    .WaitFor(rustfs)
    .WithHttpEndpoint(7010)
    .WithHttpsEndpoint(7011);

builder.Build().Run();