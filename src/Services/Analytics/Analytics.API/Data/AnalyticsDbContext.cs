using Analytics.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Analytics.API.Data;

public class AnalyticsDbContext : DbContext
{
    public AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options) : base(options) { }

    public DbSet<AnomalyRecord> Anomalies => Set<AnomalyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AnomalyRecord>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.EquipmentId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EquipmentName).HasMaxLength(200);
            entity.Property(e => e.MetricType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Method).IsRequired().HasMaxLength(30);
            entity.Property(e => e.Severity).IsRequired().HasMaxLength(30);

            // Fast lookup by equipment + time (most common query)
            entity.HasIndex(e => new { e.EquipmentId, e.DetectedAt });
            // Dashboard: unacknowledged anomalies
            entity.HasIndex(e => new { e.IsAcknowledged, e.DetectedAt });
        });
    }
}
