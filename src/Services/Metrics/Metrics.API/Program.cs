using BuildingBlocks.Common.Extensions;
using Metrics.API.Data;
using Metrics.API.Services;
using Microsoft.EntityFrameworkCore;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<MetricsDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("MetricsDb"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(maxRetryCount: 3)));

builder.Services.AddScoped<IMetricsService, MetricsService>();

var rabbitHost = builder.Configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost";
builder.Services.AddRabbitMqEventBus(rabbitHost);

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Metrics API", Version = "v1",
        Description = "Ingests, stores, and queries factory metrics data (OEE, throughput, temperature, etc.)." });
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<MetricsDbContext>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyMethod()
              .AllowAnyHeader());
});

var app = builder.Build();

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

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<MetricsDbContext>();
    try
    {
        context.Database.EnsureCreated();
        app.Logger.LogInformation("Metrics database initialized successfully");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not initialize Metrics database");
    }
}

app.Run();
