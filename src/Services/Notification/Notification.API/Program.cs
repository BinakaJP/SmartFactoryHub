using BuildingBlocks.Common.Extensions;
using Microsoft.EntityFrameworkCore;
using Notification.API.Consumers;
using Notification.API.Data;
using Notification.API.Hubs;
using Notification.API.Services;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// ----- Services -----

// Entity Framework Core with SQL Server
builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("NotificationDb"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(maxRetryCount: 3)));

// Business services
builder.Services.AddScoped<INotificationService, NotificationService>();

// RabbitMQ event bus (from BuildingBlocks.Common)
var rabbitHost = builder.Configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost";
var rabbitUser = builder.Configuration.GetValue<string>("RabbitMQ:Username") ?? "guest";
var rabbitPass = builder.Configuration.GetValue<string>("RabbitMQ:Password") ?? "guest";
builder.Services.AddRabbitMqEventBus(rabbitHost, rabbitUser, rabbitPass);

// Background consumers — listen for events from RabbitMQ
builder.Services.AddHostedService<AlertTriggeredConsumer>();
builder.Services.AddHostedService<EquipmentStatusChangedConsumer>();

// SignalR — real-time push to Angular
builder.Services.AddSignalR();

// Controllers + Swagger
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Notification API",
        Version = "v1",
        Description = "Delivers real-time factory notifications via SignalR. Listens for alert and equipment status events from RabbitMQ."
    });
});

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<NotificationDbContext>();

// CORS — must allow credentials for SignalR WebSocket connections
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()); // Required for SignalR
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
app.MapHub<FactoryNotificationsHub>("/hubs/factory");
app.MapHealthChecks("/health");
app.MapMetrics();

// Auto-create database on startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    try
    {
        context.Database.EnsureCreated();
        app.Logger.LogInformation("Notification database initialized successfully");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not initialize Notification database. Will retry on first request.");
    }
}

app.Logger.LogInformation("Notification.API starting on {Urls}", string.Join(", ", app.Urls));
app.Run();

// Expose Program class for WebApplicationFactory in integration tests
public partial class Program { }
