using BuildingBlocks.Common.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Integration.Tests.Helpers;

/// <summary>
/// Factory helpers for creating in-process test servers for each service.
/// Each factory replaces SQL Server with an isolated InMemory database and
/// mocks out the RabbitMQ event bus so tests run without infrastructure.
///
/// IMPORTANT: Uses ConfigureTestServices (runs AFTER Program.cs services) so
/// our InMemory DbContext correctly replaces the SQL Server one registered
/// in each service's Program.cs.
///
/// IMPORTANT: The InMemory database name MUST be captured in a local variable
/// before the lambda — Guid.NewGuid() inside the lambda would be evaluated on
/// every scope creation, giving each scope a different (empty) database and
/// causing seeded test data to be invisible to HTTP requests.
/// </summary>

// ── Identity API ─────────────────────────────────────────────────────────────

public class IdentityApiFactory : WebApplicationFactory<Identity.API.ApiEntryPoint>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Supply required JWT config so Program.cs doesn't throw on startup
        builder.UseSetting("Jwt:SecretKey", "integration-test-jwt-secret-that-is-32-chars!!");
        builder.UseSetting("Jwt:Issuer",    "test-issuer");
        builder.UseSetting("Jwt:Audience",  "test-audience");
        builder.UseSetting("Jwt:ExpiryMinutes", "60");

        // Capture DB name BEFORE lambda so all scopes share the same database
        var dbName = $"IdentityIntegration_{Guid.NewGuid()}";

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<Identity.API.Data.IdentityDbContext>>();
            services.RemoveAll<Identity.API.Data.IdentityDbContext>();
            services.AddDbContext<Identity.API.Data.IdentityDbContext>(options =>
                options.UseInMemoryDatabase(dbName));
        });

        builder.UseEnvironment("Testing");
    }
}

// ── Alert API ─────────────────────────────────────────────────────────────────

public class AlertApiFactory : WebApplicationFactory<Alert.API.ApiEntryPoint>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var dbName = $"AlertIntegration_{Guid.NewGuid()}";

        builder.ConfigureTestServices(services =>
        {
            // Replace SQL Server DbContext with InMemory (same name for all scopes)
            services.RemoveAll<DbContextOptions<Alert.API.Data.AlertDbContext>>();
            services.RemoveAll<Alert.API.Data.AlertDbContext>();
            services.AddDbContext<Alert.API.Data.AlertDbContext>(options =>
                options.UseInMemoryDatabase(dbName));

            // Replace RabbitMQ event bus with a no-op mock
            services.RemoveAll<IEventBus>();
            services.AddSingleton<IEventBus>(_ =>
            {
                var mock = new Mock<IEventBus>();
                mock.Setup(e => e.PublishAsync(It.IsAny<BuildingBlocks.Common.Events.IntegrationEvent>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);
                return mock.Object;
            });

            // Remove background consumers that require a live RabbitMQ connection
            var consumersToRemove = services
                .Where(s => s.ImplementationType == typeof(Alert.API.Consumers.MetricThresholdBreachedConsumer))
                .ToList();
            foreach (var d in consumersToRemove) services.Remove(d);
        });

        builder.UseEnvironment("Testing");
    }
}

// ── Metrics API ───────────────────────────────────────────────────────────────

public class MetricsApiFactory : WebApplicationFactory<Metrics.API.ApiEntryPoint>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var dbName = $"MetricsIntegration_{Guid.NewGuid()}";

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<Metrics.API.Data.MetricsDbContext>>();
            services.RemoveAll<Metrics.API.Data.MetricsDbContext>();
            services.AddDbContext<Metrics.API.Data.MetricsDbContext>(options =>
                options.UseInMemoryDatabase(dbName));

            services.RemoveAll<IEventBus>();
            services.AddSingleton<IEventBus>(_ =>
            {
                var mock = new Mock<IEventBus>();
                mock.Setup(e => e.PublishAsync(It.IsAny<BuildingBlocks.Common.Events.IntegrationEvent>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);
                return mock.Object;
            });
        });

        builder.UseEnvironment("Testing");
    }
}

// ── Equipment API ─────────────────────────────────────────────────────────────

public class EquipmentApiFactory : WebApplicationFactory<Equipment.API.ApiEntryPoint>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var dbName = $"EquipmentIntegration_{Guid.NewGuid()}";

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<Equipment.API.Data.EquipmentDbContext>>();
            services.RemoveAll<Equipment.API.Data.EquipmentDbContext>();
            services.AddDbContext<Equipment.API.Data.EquipmentDbContext>(options =>
                options.UseInMemoryDatabase(dbName));

            services.RemoveAll<IEventBus>();
            services.AddSingleton<IEventBus>(_ =>
            {
                var mock = new Mock<IEventBus>();
                mock.Setup(e => e.PublishAsync(It.IsAny<BuildingBlocks.Common.Events.IntegrationEvent>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);
                return mock.Object;
            });
        });

        builder.UseEnvironment("Testing");
    }
}

// ── Notification API ──────────────────────────────────────────────────────────

public class NotificationApiFactory : WebApplicationFactory<Notification.API.ApiEntryPoint>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var dbName = $"NotificationIntegration_{Guid.NewGuid()}";

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<Notification.API.Data.NotificationDbContext>>();
            services.RemoveAll<Notification.API.Data.NotificationDbContext>();
            services.AddDbContext<Notification.API.Data.NotificationDbContext>(options =>
                options.UseInMemoryDatabase(dbName));

            services.RemoveAll<IEventBus>();
            services.AddSingleton<IEventBus>(_ =>
            {
                var mock = new Mock<IEventBus>();
                mock.Setup(e => e.PublishAsync(It.IsAny<BuildingBlocks.Common.Events.IntegrationEvent>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);
                return mock.Object;
            });

            // Remove background consumers that require a live RabbitMQ connection
            var consumersToRemove = services
                .Where(s =>
                    s.ImplementationType == typeof(Notification.API.Consumers.AlertTriggeredConsumer) ||
                    s.ImplementationType == typeof(Notification.API.Consumers.EquipmentStatusChangedConsumer))
                .ToList();
            foreach (var d in consumersToRemove) services.Remove(d);
        });

        builder.UseEnvironment("Testing");
    }
}
