using Alert.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Alert.API.Data;

public class AlertDbContext : DbContext
{
    public AlertDbContext(DbContextOptions<AlertDbContext> options) : base(options) { }

    public DbSet<Alert.API.Models.Alert> Alerts => Set<Alert.API.Models.Alert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Alert.API.Models.Alert>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EquipmentId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EquipmentName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.MetricType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.AcknowledgedBy).HasMaxLength(200);
            entity.Property(e => e.ResolutionNote).HasMaxLength(1000);

            entity.Property(e => e.Severity).HasConversion<string>();
            entity.Property(e => e.Status).HasConversion<string>();

            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.Severity);
            entity.HasIndex(e => e.EquipmentId);
            entity.HasIndex(e => e.CreatedAt);
        });
    }
}
