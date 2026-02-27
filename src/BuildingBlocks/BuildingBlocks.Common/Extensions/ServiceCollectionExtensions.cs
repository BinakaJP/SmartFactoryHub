using BuildingBlocks.Common.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Common.Extensions;

/// <summary>
/// Extension methods for registering BuildingBlocks services in the DI container.
/// Used by all microservices for consistent setup.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the RabbitMQ event bus as a singleton.
    /// </summary>
    public static IServiceCollection AddRabbitMqEventBus(this IServiceCollection services, string hostName = "localhost")
    {
        services.AddSingleton<IEventBus>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RabbitMqEventBus>>();
            return new RabbitMqEventBus(logger, hostName);
        });

        return services;
    }
}
