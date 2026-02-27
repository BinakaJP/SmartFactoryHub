using Simulator.Service.Simulators;
using Simulator.Service.Workers;

var builder = Host.CreateApplicationBuilder(args);

var metricsApiUrl = builder.Configuration.GetValue<string>("Simulator:MetricsApiUrl") ?? "http://metrics-api:8080";

builder.Services.AddHttpClient<FactorySimulator>(client =>
{
    client.BaseAddress = new Uri(metricsApiUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHostedService<SimulatorWorker>();

var host = builder.Build();
host.Run();
