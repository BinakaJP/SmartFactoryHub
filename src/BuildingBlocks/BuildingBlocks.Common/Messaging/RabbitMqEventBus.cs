using System.Text;
using System.Text.Json;
using BuildingBlocks.Common.Events;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BuildingBlocks.Common.Messaging;

/// <summary>
/// RabbitMQ implementation of IEventBus.
/// Uses a topic exchange so that multiple services can subscribe to the same event type.
/// </summary>
public class RabbitMqEventBus : IEventBus, IAsyncDisposable
{
    private readonly ILogger<RabbitMqEventBus> _logger;
    private readonly string _hostName;
    private readonly string _userName;
    private readonly string _password;
    private readonly string _exchangeName = "smartfactory_events";
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqEventBus(
        ILogger<RabbitMqEventBus> logger,
        string hostName = "localhost",
        string userName = "guest",
        string password = "guest")
    {
        _logger = logger;
        _hostName = hostName;
        _userName = userName;
        _password = password;
    }

    private async Task EnsureConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true })
            return;

        var factory = new ConnectionFactory
        {
            HostName = _hostName,
            UserName = _userName,
            Password = _password,
            AutomaticRecoveryEnabled = true
        };

        _connection = await factory.CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await _channel.ExchangeDeclareAsync(
            exchange: _exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Connected to RabbitMQ at {Host}", _hostName);
    }

    public async Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : IntegrationEvent
    {
        await EnsureConnectionAsync(cancellationToken);

        var routingKey = typeof(T).Name;
        var message = JsonSerializer.Serialize(@event);
        var body = Encoding.UTF8.GetBytes(message);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = @event.Id.ToString(),
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        await _channel!.BasicPublishAsync(
            exchange: _exchangeName,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Published event {EventType} with ID {EventId}", routingKey, @event.Id);
    }

    public async Task SubscribeAsync<T>(Func<T, Task> handler, CancellationToken cancellationToken = default) where T : IntegrationEvent
    {
        await EnsureConnectionAsync(cancellationToken);

        var routingKey = typeof(T).Name;
        var queueName = $"{routingKey}_queue";

        await _channel!.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await _channel.QueueBindAsync(
            queue: queueName,
            exchange: _exchangeName,
            routingKey: routingKey,
            cancellationToken: cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                var @event = JsonSerializer.Deserialize<T>(body);

                if (@event != null)
                {
                    _logger.LogInformation("Received event {EventType} with ID {EventId}", routingKey, @event.Id);
                    await handler(@event);
                }

                await _channel.BasicAckAsync(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing event {EventType}", routingKey);
                await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Subscribed to {EventType} on queue {QueueName}", routingKey, queueName);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is { IsOpen: true })
            await _channel.CloseAsync();

        if (_connection is { IsOpen: true })
            await _connection.CloseAsync();

        GC.SuppressFinalize(this);
    }
}
