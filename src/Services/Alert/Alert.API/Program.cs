using Alert.API.Consumers;
using Alert.API.Data;
using Alert.API.Services;
using BuildingBlocks.Common.Extensions;
using Microsoft.EntityFrameworkCore;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// ----- Services -----

// Entity Framework Core with SQL Server
builder.Services.AddDbContext<AlertDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("AlertDb"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(maxRetryCount: 3)));

// Business services
builder.Services.AddScoped<IAlertService, AlertService>();

// RabbitMQ event bus (from BuildingBlocks.Common)
var rabbitHost = builder.Configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost";
var rabbitUser = builder.Configuration.GetValue<string>("RabbitMQ:Username") ?? "guest";
var rabbitPass = builder.Configuration.GetValue<string>("RabbitMQ:Password") ?? "guest";
builder.Services.AddRabbitMqEventBus(rabbitHost, rabbitUser, rabbitPass);

// Background consumer — listens for MetricThresholdBreachedEvent
builder.Services.AddHostedService<MetricThresholdBreachedConsumer>();

// Controllers + Swagger — enums as strings
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Alert API",
        Version = "v1",
        Description = "Manages factory alert lifecycle: creation from threshold breaches, acknowledgement, and resolution."
    });
});

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AlertDbContext>();

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
    var context = scope.ServiceProvider.GetRequiredService<AlertDbContext>();
    try
    {
        context.Database.EnsureCreated();
        app.Logger.LogInformation("Alert database initialized successfully");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not initialize Alert database. Will retry on first request.");
    }
}

app.Logger.LogInformation("Alert.API starting on {Urls}", string.Join(", ", app.Urls));
app.Run();
