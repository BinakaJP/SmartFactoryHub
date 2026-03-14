using Analytics.API.Services;
using BuildingBlocks.Common.Events;
using BuildingBlocks.Common.Messaging;

namespace Analytics.API.Consumers;

/// <summary>
/// Background service that listens for MetricRecordedEvent from RabbitMQ and
/// passes each metric through the analytics engine for anomaly detection and
/// health scoring.
/// </summary>
public class MetricRecordedConsumer : BackgroundService
{
    private readonly IEventBus         _eventBus;
    private readonly IServiceProvider  _serviceProvider;
    private readonly ILogger<MetricRecordedConsumer> _logger;

    public MetricRecordedConsumer(
        IEventBus eventBus,
        IServiceProvider serviceProvider,
        ILogger<MetricRecordedConsumer> logger)
    {
        _eventBus        = eventBus;
        _serviceProvider = serviceProvider;
        _logger          = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MetricRecordedConsumer starting — subscribing to RabbitMQ...");

        await _eventBus.SubscribeAsync<MetricRecordedEvent>(
            async @event =>
            {
                using var scope = _serviceProvider.CreateScope();
                var analyticsService = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();
                await analyticsService.ProcessMetricAsync(@event);
            },
            stoppingToken);

        _logger.LogInformation("MetricRecordedConsumer subscribed and listening");

        // Keep the background service alive until cancellation
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
