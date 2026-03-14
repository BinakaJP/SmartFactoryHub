using Analytics.API.Consumers;
using Analytics.API.Core;
using Analytics.API.Data;
using Analytics.API.Services;
using Analytics.API.Workers;
using BuildingBlocks.Common.Extensions;
using Microsoft.EntityFrameworkCore;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// ----- Services -----

// Entity Framework Core with SQL Server
builder.Services.AddDbContext<AnalyticsDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("AnalyticsDb"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(maxRetryCount: 3)));

// Singleton analytics engine — holds all in-memory rolling windows and health history
builder.Services.AddSingleton<AnalyticsEngine>();

// Scoped analytics service — uses DbContext + engine + event bus
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();

// RabbitMQ event bus (from BuildingBlocks.Common)
var rabbitHost = builder.Configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost";
var rabbitUser = builder.Configuration.GetValue<string>("RabbitMQ:Username") ?? "guest";
var rabbitPass = builder.Configuration.GetValue<string>("RabbitMQ:Password") ?? "guest";
builder.Services.AddRabbitMqEventBus(rabbitHost, rabbitUser, rabbitPass);

// Background consumer — listens for MetricRecordedEvent
builder.Services.AddHostedService<MetricRecordedConsumer>();

// HTTP client for startup seeding from Metrics.API
builder.Services.AddHttpClient("MetricsApi");

// Startup worker — pre-warms rolling windows from historical metric data
builder.Services.AddHostedService<MetricsSeedWorker>();

// Controllers + Swagger — enums as strings
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title       = "Analytics API",
        Version     = "v1",
        Description = "Real-time anomaly detection and predictive maintenance analytics for the Smart Factory Hub. " +
                      "Runs Z-Score, EWMA, and Rate-of-Change detection on every incoming metric and estimates " +
                      "Remaining Useful Life (RUL) via linear regression on rolling health scores."
    });
});

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AnalyticsDbContext>();

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

app.UseHttpMetrics();
app.UseRouting();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapMetrics();

// Auto-create database on startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
    try
    {
        context.Database.EnsureCreated();
        app.Logger.LogInformation("Analytics database initialized successfully");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not initialize Analytics database. Will retry on first request.");
    }
}

app.Logger.LogInformation("Analytics.API starting on {Urls}", string.Join(", ", app.Urls));
app.Run();
