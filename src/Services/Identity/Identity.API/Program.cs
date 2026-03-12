using System.Text;
using Identity.API.Data;
using Identity.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// ----- Services -----

// Entity Framework Core with SQL Server
builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("IdentityDb"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(maxRetryCount: 3)));

// Business services
builder.Services.AddScoped<IAuthService, AuthService>();

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("Jwt:SecretKey must be configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

// Controllers + Swagger with JWT support
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Identity API",
        Version = "v1",
        Description = "Centralized authentication service. Issues JWT tokens for SmartFactory Hub. Roles: Admin, Engineer, Operator, Viewer. Use POST /api/auth/login to get a Bearer token, then set Authorization header."
    });
});

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<IdentityDbContext>();

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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapMetrics();

// Auto-create database on startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    try
    {
        context.Database.EnsureCreated();
        app.Logger.LogInformation("Identity database initialized with seed users");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not initialize Identity database. Will retry on first request.");
    }
}

app.Logger.LogInformation("Identity.API starting on {Urls}", string.Join(", ", app.Urls));
app.Run();

// Expose Program class for WebApplicationFactory in integration tests
public partial class Program { }
