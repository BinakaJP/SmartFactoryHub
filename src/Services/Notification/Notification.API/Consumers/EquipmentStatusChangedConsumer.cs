using BuildingBlocks.Common.Events;
using BuildingBlocks.Common.Messaging;
using Notification.API.Services;

namespace Notification.API.Consumers;

/// <summary>
/// Background service that listens for EquipmentStatusChangedEvent from RabbitMQ
/// and dispatches notifications when equipment goes Down or into Maintenance.
/// </summary>
public class EquipmentStatusChangedConsumer : BackgroundService
{
    private readonly IEventBus _eventBus;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EquipmentStatusChangedConsumer> _logger;

    public EquipmentStatusChangedConsumer(
        IEventBus eventBus,
        IServiceProvider serviceProvider,
        ILogger<EquipmentStatusChangedConsumer> logger)
    {
        _eventBus = eventBus;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EquipmentStatusChangedConsumer starting — subscribing to RabbitMQ...");

        await _eventBus.SubscribeAsync<EquipmentStatusChangedEvent>(
            async @event =>
            {
                using var scope = _serviceProvider.CreateScope();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                await notificationService.HandleEquipmentStatusChangedAsync(@event);
            },
            stoppingToken);

        _logger.LogInformation("EquipmentStatusChangedConsumer subscribed and listening");
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
