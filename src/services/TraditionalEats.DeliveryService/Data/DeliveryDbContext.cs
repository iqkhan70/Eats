using Microsoft.EntityFrameworkCore;
using TraditionalEats.DeliveryService.Entities;

namespace TraditionalEats.DeliveryService.Data;

public class DeliveryDbContext : DbContext
{
    public DeliveryDbContext(DbContextOptions<DeliveryDbContext> options) : base(options)
    {
    }

    public DbSet<Driver> Drivers { get; set; }
    public DbSet<Delivery> Deliveries { get; set; }
    public DbSet<DeliveryTracking> DeliveryTracking { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Driver>(entity =>
        {
            entity.HasKey(e => e.DriverId);
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.HasIndex(e => e.IsAvailable);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.PhoneNumber).HasMaxLength(50);
            entity.Property(e => e.VehicleType).HasMaxLength(50);
            entity.Property(e => e.VehicleLicensePlate).HasMaxLength(50);
        });

        modelBuilder.Entity<Delivery>(entity =>
        {
            entity.HasKey(e => e.DeliveryId);
            entity.HasIndex(e => e.OrderId).IsUnique();
            entity.HasIndex(e => e.DriverId);
            entity.HasIndex(e => e.Status);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PickupAddress).HasMaxLength(500);
            entity.Property(e => e.DeliveryAddress).HasMaxLength(500);
            entity.HasOne(e => e.Driver)
                .WithMany(d => d.Deliveries)
                .HasForeignKey(e => e.DriverId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DeliveryTracking>(entity =>
        {
            entity.HasKey(e => e.TrackingId);
            entity.HasIndex(e => e.DeliveryId);
            entity.HasIndex(e => e.Timestamp);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.HasOne(e => e.Delivery)
                .WithMany(d => d.TrackingHistory)
                .HasForeignKey(e => e.DeliveryId);
        });
    }
}
