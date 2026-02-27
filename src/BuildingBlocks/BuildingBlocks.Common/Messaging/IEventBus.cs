using BuildingBlocks.Common.Events;

namespace BuildingBlocks.Common.Messaging;

/// <summary>
/// Abstraction over the message broker (RabbitMQ).
/// Allows publishing and subscribing to integration events across microservices.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publishes an integration event to the message broker.
    /// The event is serialized and sent to a RabbitMQ exchange.
    /// </summary>
    Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : IntegrationEvent;

    /// <summary>
    /// Subscribes to an integration event type.
    /// When an event of type T is received, the handler is invoked.
    /// </summary>
    Task SubscribeAsync<T>(Func<T, Task> handler, CancellationToken cancellationToken = default) where T : IntegrationEvent;
}
