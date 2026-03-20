using Chatbot.API.Plugins;
using Chatbot.API.Services;
using Microsoft.SemanticKernel;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// ── Named HTTP clients for each internal service ─────────────────────────────
var serviceUrls = builder.Configuration.GetSection("ServiceUrls");

builder.Services.AddHttpClient("EquipmentApi", c =>
    c.BaseAddress = new Uri(serviceUrls["EquipmentApi"] ?? "http://equipment-api:8080"));

builder.Services.AddHttpClient("MetricsApi", c =>
    c.BaseAddress = new Uri(serviceUrls["MetricsApi"] ?? "http://metrics-api:8080"));

builder.Services.AddHttpClient("AlertApi", c =>
    c.BaseAddress = new Uri(serviceUrls["AlertApi"] ?? "http://alert-api:8080"));

builder.Services.AddHttpClient("AnalyticsApi", c =>
    c.BaseAddress = new Uri(serviceUrls["AnalyticsApi"] ?? "http://analytics-api:8080"));

// ── Plugins (singletons — stateless, share IHttpClientFactory) ────────────────
builder.Services.AddSingleton<EquipmentPlugin>();
builder.Services.AddSingleton<MetricsPlugin>();
builder.Services.AddSingleton<AlertPlugin>();
builder.Services.AddSingleton<AnalyticsPlugin>();

// ── Semantic Kernel ───────────────────────────────────────────────────────────
var aiProvider = builder.Configuration.GetValue<string>("AiProvider") ?? "None";

builder.Services.AddSingleton(sp =>
{
    var kernelBuilder = Kernel.CreateBuilder();

    if (string.Equals(aiProvider, "AzureOpenAI", StringComparison.OrdinalIgnoreCase))
    {
        var endpoint = builder.Configuration["AzureOpenAI:Endpoint"]!;
        var apiKey = builder.Configuration["AzureOpenAI:ApiKey"]!;
        var deployment = builder.Configuration["AzureOpenAI:DeploymentName"] ?? "gpt-4o";
        kernelBuilder.AddAzureOpenAIChatCompletion(deployment, endpoint, apiKey);
    }
    else if (string.Equals(aiProvider, "OpenAI", StringComparison.OrdinalIgnoreCase))
    {
        var apiKey = builder.Configuration["OpenAI:ApiKey"]!;
        var modelId = builder.Configuration["OpenAI:ModelId"] ?? "gpt-4o";
        kernelBuilder.AddOpenAIChatCompletion(modelId, apiKey);
    }
    // else: "None" — kernel built without AI; ChatService will handle gracefully

    var kernel = kernelBuilder.Build();

    // Register plugins from DI
    kernel.Plugins.AddFromObject(sp.GetRequiredService<EquipmentPlugin>(), "Equipment");
    kernel.Plugins.AddFromObject(sp.GetRequiredService<MetricsPlugin>(), "Metrics");
    kernel.Plugins.AddFromObject(sp.GetRequiredService<AlertPlugin>(), "Alerts");
    kernel.Plugins.AddFromObject(sp.GetRequiredService<AnalyticsPlugin>(), "Analytics");

    return kernel;
});

// ── Session manager (singleton) and chat service (scoped) ─────────────────────
builder.Services.AddSingleton<ISessionManager, SessionManager>();
builder.Services.AddScoped<IChatService, ChatService>();

// ── Infrastructure ────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Chatbot.API", Version = "v1" });
});

builder.Services.AddHealthChecks();

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

// ── Pipeline ──────────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.UseRouting();
app.UseHttpMetrics();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapMetrics("/metrics");

app.Run();
