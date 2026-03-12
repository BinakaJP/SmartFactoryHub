using BuildingBlocks.Common.Extensions;
using Equipment.API.Data;
using Equipment.API.Services;
using Microsoft.EntityFrameworkCore;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// ----- Services -----

// Entity Framework Core with SQL Server
builder.Services.AddDbContext<EquipmentDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("EquipmentDb"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(maxRetryCount: 3)));

// Business services
builder.Services.AddScoped<IEquipmentService, EquipmentService>();

// RabbitMQ event bus (from BuildingBlocks.Common)
var rabbitHost = builder.Configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost";
var rabbitUser = builder.Configuration.GetValue<string>("RabbitMQ:Username") ?? "guest";
var rabbitPass = builder.Configuration.GetValue<string>("RabbitMQ:Password") ?? "guest";
builder.Services.AddRabbitMqEventBus(rabbitHost, rabbitUser, rabbitPass);

// Controllers + Swagger — enums serialized as strings (e.g. "Running" not 2)
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Equipment API", Version = "v1",
        Description = "Manages factory equipment registry, status tracking, and lifecycle." });
});

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<EquipmentDbContext>();

// CORS (for Angular frontend)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyMethod()
              .AllowAnyHeader());
});

var app = builder.Build();

// ----- Middleware Pipeline -----

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");

// Prometheus metrics endpoint (/metrics)
app.UseHttpMetrics();
app.UseRouting();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapMetrics(); // Exposes /metrics for Prometheus scraping

// Auto-create database on startup (development only)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<EquipmentDbContext>();
    try
    {
        context.Database.EnsureCreated();
        app.Logger.LogInformation("Equipment database initialized successfully");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not initialize database. Will retry on first request.");
    }
}

app.Logger.LogInformation("Equipment.API starting on {Urls}", string.Join(", ", app.Urls));
app.Run();

// Expose Program class for WebApplicationFactory in integration tests
public partial class Program { }
