using BuildingBlocks.Common.Events;
using BuildingBlocks.Common.Messaging;
using Notification.API.Services;

namespace Notification.API.Consumers;

/// <summary>
/// Background service that listens for MaintenancePredictedEvent from RabbitMQ
/// (published by Analytics.API) and dispatches maintenance prediction notifications
/// via SignalR and the notification log.
/// </summary>
public class MaintenancePredictedConsumer : BackgroundService
{
    private readonly IEventBus        _eventBus;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MaintenancePredictedConsumer> _logger;

    public MaintenancePredictedConsumer(
        IEventBus eventBus,
        IServiceProvider serviceProvider,
        ILogger<MaintenancePredictedConsumer> logger)
    {
        _eventBus        = eventBus;
        _serviceProvider = serviceProvider;
        _logger          = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MaintenancePredictedConsumer starting — subscribing to RabbitMQ...");

        await _eventBus.SubscribeAsync<MaintenancePredictedEvent>(
            async @event =>
            {
                using var scope = _serviceProvider.CreateScope();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                await notificationService.HandleMaintenancePredictedAsync(@event);
            },
            stoppingToken);

        _logger.LogInformation("MaintenancePredictedConsumer subscribed and listening");
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
