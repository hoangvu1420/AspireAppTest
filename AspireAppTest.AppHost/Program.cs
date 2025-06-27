var builder = DistributedApplication.CreateBuilder(args);

// builder.AddAzureContainerAppEnvironment("acaEnv")
//     .WithAzdResourceNaming();

var isPublishMode = builder.ExecutionContext.IsPublishMode;

var cache = isPublishMode
    ? builder.AddAzureRedis("cache")
    : builder.AddAzureRedis("cache").RunAsContainer();

// 1. Declare the builder itself first
var postgresBuilder = builder
    .AddAzurePostgresFlexibleServer("postgres");

// 2. In publish mode, wire up password auth; otherwise, spin up a local container
if (isPublishMode)
{
    // these only exist in publish mode
    var userParam = builder.AddParameter("postgresUser", secret: true);
    var passwordParam = builder.AddParameter("postgresPassword", secret: true);

    postgresBuilder = postgresBuilder
        .WithPasswordAuthentication(userParam, passwordParam);
}
else
{
    // local runs fallback to Docker Postgres
    postgresBuilder = postgresBuilder.RunAsContainer();
}

// 3. Build the final resource and database
var postgres = postgresBuilder;
var db = postgres.AddDatabase("LMSdb");

var apiService = builder.AddProject<Projects.AspireAppTest_ApiService>("apiservice")
    .WithExternalHttpEndpoints()
    .WithReference(db)
    .WaitFor(postgres)
    .WithReference(cache)
    .WaitFor(cache);

builder.AddProject<Projects.AspireAppTest_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
