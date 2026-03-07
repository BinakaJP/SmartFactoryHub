using BuildingBlocks.Common.Events;
using BuildingBlocks.Common.Messaging;
using Notification.API.Services;

namespace Notification.API.Consumers;

/// <summary>
/// Background service that listens for AlertTriggeredEvent from RabbitMQ
/// and dispatches notifications via SignalR.
/// </summary>
public class AlertTriggeredConsumer : BackgroundService
{
    private readonly IEventBus _eventBus;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AlertTriggeredConsumer> _logger;

    public AlertTriggeredConsumer(
        IEventBus eventBus,
        IServiceProvider serviceProvider,
        ILogger<AlertTriggeredConsumer> logger)
    {
        _eventBus = eventBus;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AlertTriggeredConsumer starting — subscribing to RabbitMQ...");

        await _eventBus.SubscribeAsync<AlertTriggeredEvent>(
            async @event =>
            {
                using var scope = _serviceProvider.CreateScope();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                await notificationService.HandleAlertTriggeredAsync(@event);
            },
            stoppingToken);

        _logger.LogInformation("AlertTriggeredConsumer subscribed and listening");
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
