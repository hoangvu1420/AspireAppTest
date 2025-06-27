using AspireAppTest.ApiService.Data;
using AspireAppTest.ApiService.Endpoints;
using AspireAppTest.ApiService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

builder.AddNpgsqlDbContext<LibraryDbContext>("LMSdb");

builder.AddRedisDistributedCache("cache");

builder.Services.AddScoped<IBookService, BookService>();

builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapBookEndpoints();

app.MapWeatherEndpoints();

app.MapDefaultEndpoints();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    await DbInitializer.InitializeAsync(context, logger, configuration);
}

app.Run();
