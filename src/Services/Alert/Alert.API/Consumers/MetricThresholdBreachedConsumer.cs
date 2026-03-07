using Alert.API.Services;
using BuildingBlocks.Common.Events;
using BuildingBlocks.Common.Messaging;

namespace Alert.API.Consumers;

/// <summary>
/// Background service that listens for MetricThresholdBreachedEvent from RabbitMQ
/// and creates alerts via AlertService.
/// </summary>
public class MetricThresholdBreachedConsumer : BackgroundService
{
    private readonly IEventBus _eventBus;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MetricThresholdBreachedConsumer> _logger;

    public MetricThresholdBreachedConsumer(
        IEventBus eventBus,
        IServiceProvider serviceProvider,
        ILogger<MetricThresholdBreachedConsumer> logger)
    {
        _eventBus = eventBus;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MetricThresholdBreachedConsumer starting — subscribing to RabbitMQ...");

        await _eventBus.SubscribeAsync<MetricThresholdBreachedEvent>(
            async @event =>
            {
                using var scope = _serviceProvider.CreateScope();
                var alertService = scope.ServiceProvider.GetRequiredService<IAlertService>();
                await alertService.CreateFromEventAsync(@event);
            },
            stoppingToken);

        _logger.LogInformation("MetricThresholdBreachedConsumer subscribed and listening");

        // Keep the background service alive until cancellation
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
