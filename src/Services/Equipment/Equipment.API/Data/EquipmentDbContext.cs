using Equipment.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Equipment.API.Data;

/// <summary>
/// Entity Framework DbContext for the Equipment microservice.
/// Each microservice owns its own database (database-per-service pattern).
/// </summary>
public class EquipmentDbContext : DbContext
{
    public EquipmentDbContext(DbContextOptions<EquipmentDbContext> options) : base(options) { }

    public DbSet<EquipmentEntity> Equipment => Set<EquipmentEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EquipmentEntity>(entity =>
        {
            entity.ToTable("Equipment");
            entity.HasIndex(e => e.EquipmentCode).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.Location);
            entity.HasIndex(e => e.ProductionLine);

            entity.Property(e => e.Status)
                  .HasConversion<string>();

            entity.Property(e => e.Type)
                  .HasConversion<string>();
        });

        // Seed some initial equipment for demonstration
        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EquipmentEntity>().HasData(
            new EquipmentEntity
            {
                Id = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
                EquipmentCode = "CNC-001",
                Name = "CNC Milling Machine Alpha",
                Description = "High-precision 5-axis CNC milling machine for metal components",
                Type = EquipmentType.ProductionMachine,
                Status = EquipmentStatus.Running,
                Location = "Building A",
                PlantArea = "Machining Center",
                ProductionLine = "Line-1",
                Manufacturer = "Haas Automation",
                ModelNumber = "VF-2SS",
                InstalledDate = new DateTime(2023, 1, 15, 0, 0, 0, DateTimeKind.Utc)
            },
            new EquipmentEntity
            {
                Id = Guid.Parse("b2c3d4e5-f6a7-8901-bcde-f12345678901"),
                EquipmentCode = "CONV-001",
                Name = "Main Assembly Conveyor",
                Description = "Primary conveyor belt for the assembly line",
                Type = EquipmentType.ConveyorBelt,
                Status = EquipmentStatus.Running,
                Location = "Building A",
                PlantArea = "Assembly Area",
                ProductionLine = "Line-1",
                Manufacturer = "Siemens",
                ModelNumber = "CB-500",
                InstalledDate = new DateTime(2022, 6, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new EquipmentEntity
            {
                Id = Guid.Parse("c3d4e5f6-a7b8-9012-cdef-123456789012"),
                EquipmentCode = "TEMP-001",
                Name = "Furnace Temperature Sensor",
                Description = "High-temperature thermocouple sensor for heat treatment furnace",
                Type = EquipmentType.Sensor,
                Status = EquipmentStatus.Running,
                Location = "Building B",
                PlantArea = "Heat Treatment",
                ProductionLine = "Line-2",
                Manufacturer = "Omega Engineering",
                ModelNumber = "TC-K500",
                InstalledDate = new DateTime(2023, 3, 10, 0, 0, 0, DateTimeKind.Utc)
            },
            new EquipmentEntity
            {
                Id = Guid.Parse("d4e5f6a7-b8c9-0123-defa-234567890123"),
                EquipmentCode = "ROB-001",
                Name = "Welding Robot Arm",
                Description = "6-axis robotic welding arm for chassis assembly",
                Type = EquipmentType.Robot,
                Status = EquipmentStatus.Idle,
                Location = "Building A",
                PlantArea = "Welding Bay",
                ProductionLine = "Line-1",
                Manufacturer = "FANUC",
                ModelNumber = "ARC Mate 100iD",
                InstalledDate = new DateTime(2023, 8, 20, 0, 0, 0, DateTimeKind.Utc)
            },
            new EquipmentEntity
            {
                Id = Guid.Parse("e5f6a7b8-c9d0-1234-efab-345678901234"),
                EquipmentCode = "QC-001",
                Name = "Vision Quality Inspector",
                Description = "AI-powered visual inspection system for defect detection",
                Type = EquipmentType.QualityInspection,
                Status = EquipmentStatus.Running,
                Location = "Building A",
                PlantArea = "Quality Control",
                ProductionLine = "Line-1",
                Manufacturer = "Cognex",
                ModelNumber = "IS-9000",
                InstalledDate = new DateTime(2024, 1, 5, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}
