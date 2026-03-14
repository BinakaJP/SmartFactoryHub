using BuildingBlocks.Common.Events;
using BuildingBlocks.Common.Messaging;
using Notification.API.Services;

namespace Notification.API.Consumers;

/// <summary>
/// Background service that listens for AnomalyDetectedEvent from RabbitMQ
/// (published by Analytics.API) and dispatches real-time notifications via SignalR.
/// Only Anomalous and Critical severity events are published by Analytics.API,
/// so every event received here warrants a notification.
/// </summary>
public class AnomalyDetectedConsumer : BackgroundService
{
    private readonly IEventBus        _eventBus;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AnomalyDetectedConsumer> _logger;

    public AnomalyDetectedConsumer(
        IEventBus eventBus,
        IServiceProvider serviceProvider,
        ILogger<AnomalyDetectedConsumer> logger)
    {
        _eventBus        = eventBus;
        _serviceProvider = serviceProvider;
        _logger          = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AnomalyDetectedConsumer starting — subscribing to RabbitMQ...");

        await _eventBus.SubscribeAsync<AnomalyDetectedEvent>(
            async @event =>
            {
                using var scope = _serviceProvider.CreateScope();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                await notificationService.HandleAnomalyDetectedAsync(@event);
            },
            stoppingToken);

        _logger.LogInformation("AnomalyDetectedConsumer subscribed and listening");
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
