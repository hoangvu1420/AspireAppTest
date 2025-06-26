var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

var postgres = builder.AddPostgres("postgres").AddDatabase("LMS");

var apiService = builder.AddProject<Projects.AspireAppTest_ApiService>("apiservice")
    .WithReference(postgres);

builder.AddProject<Projects.AspireAppTest_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
