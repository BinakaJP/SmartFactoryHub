using Simulator.Service.Simulators;

namespace Simulator.Service.Workers;

/// <summary>
/// Background worker that runs the factory data simulator on a configurable interval.
/// Generates realistic metric data and pushes it to the Metrics API.
/// </summary>
public class SimulatorWorker : BackgroundService
{
    private readonly ILogger<SimulatorWorker> _logger;
    private readonly FactorySimulator _simulator;
    private readonly int _intervalSeconds;

    public SimulatorWorker(ILogger<SimulatorWorker> logger, FactorySimulator simulator, IConfiguration configuration)
    {
        _logger = logger;
        _simulator = simulator;
        _intervalSeconds = configuration.GetValue<int>("Simulator:IntervalSeconds", 10);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Simulator starting with {Interval}s interval", _intervalSeconds);

        // Wait a bit for other services to start up
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _simulator.SimulateTickAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Simulator tick failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
        }

        _logger.LogInformation("Simulator stopped");
    }
}
