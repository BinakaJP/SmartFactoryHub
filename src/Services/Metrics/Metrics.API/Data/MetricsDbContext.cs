using Metrics.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Metrics.API.Data;

public class MetricsDbContext : DbContext
{
    public MetricsDbContext(DbContextOptions<MetricsDbContext> options) : base(options) { }

    public DbSet<MetricDataPoint> MetricDataPoints => Set<MetricDataPoint>();
    public DbSet<AlertThreshold> AlertThresholds => Set<AlertThreshold>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MetricDataPoint>(entity =>
        {
            entity.ToTable("MetricDataPoints");
            entity.HasIndex(e => new { e.EquipmentId, e.MetricType, e.Timestamp });
            entity.HasIndex(e => e.Timestamp);
        });

        modelBuilder.Entity<AlertThreshold>(entity =>
        {
            entity.ToTable("AlertThresholds");
            entity.HasIndex(e => new { e.EquipmentId, e.MetricType }).IsUnique();
            entity.Property(e => e.Direction).HasConversion<string>();
        });

        SeedThresholds(modelBuilder);
    }

    private static void SeedThresholds(ModelBuilder modelBuilder)
    {
        var equipmentIds = new[]
        {
            "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
            "b2c3d4e5-f6a7-8901-bcde-f12345678901",
            "c3d4e5f6-a7b8-9012-cdef-123456789012",
            "d4e5f6a7-b8c9-0123-defa-234567890123",
            "e5f6a7b8-c9d0-1234-efab-345678901234"
        };

        var thresholds = new List<AlertThreshold>();
        var counter = 0;

        foreach (var eqId in equipmentIds)
        {
            thresholds.Add(new AlertThreshold
            {
                Id = Guid.Parse($"10000000-0000-0000-0000-{counter++:D12}"),
                EquipmentId = eqId,
                MetricType = MetricTypes.Temperature,
                WarningValue = 350,
                CriticalValue = 400,
                Direction = ThresholdDirection.Above
            });
            thresholds.Add(new AlertThreshold
            {
                Id = Guid.Parse($"10000000-0000-0000-0000-{counter++:D12}"),
                EquipmentId = eqId,
                MetricType = MetricTypes.OEE,
                WarningValue = 70,
                CriticalValue = 60,
                Direction = ThresholdDirection.Below
            });
        }

        modelBuilder.Entity<AlertThreshold>().HasData(thresholds);
    }
}
