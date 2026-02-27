using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// YARP Reverse Proxy — routes requests to downstream microservices
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// CORS (for Angular frontend)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials());
});

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseCors("AllowFrontend");
app.UseHttpMetrics();
app.UseRouting();

// Map YARP reverse proxy
app.MapReverseProxy();

// Gateway health check endpoint
app.MapHealthChecks("/health");
app.MapMetrics(); // Prometheus /metrics endpoint

// Gateway info endpoint
app.MapGet("/", () => new
{
    Service = "SmartFactory API Gateway",
    Version = "1.0",
    Endpoints = new
    {
        Equipment = "/api/equipment",
        Metrics = "/api/metrics",
        Alerts = "/api/alerts",
        Health = "/health",
        PrometheusMetrics = "/metrics"
    }
});

app.Logger.LogInformation("API Gateway starting...");
app.Run();
