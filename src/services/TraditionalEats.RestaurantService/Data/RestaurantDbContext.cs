using Microsoft.EntityFrameworkCore;
using TraditionalEats.RestaurantService.Entities;

namespace TraditionalEats.RestaurantService.Data;

public class RestaurantDbContext : DbContext
{
    public RestaurantDbContext(DbContextOptions<RestaurantDbContext> options) : base(options)
    {
    }

    public DbSet<Restaurant> Restaurants { get; set; }
    public DbSet<DeliveryZone> DeliveryZones { get; set; }
    public DbSet<RestaurantHours> RestaurantHours { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.HasKey(e => e.RestaurantId);
            entity.HasIndex(e => e.OwnerId);
            entity.HasIndex(e => e.IsActive);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.CuisineType).HasMaxLength(100);
            entity.Property(e => e.PhoneNumber).HasMaxLength(50);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.Address).HasMaxLength(500);
        });

        modelBuilder.Entity<DeliveryZone>(entity =>
        {
            entity.HasKey(e => e.ZoneId);
            entity.HasIndex(e => e.RestaurantId);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.PolygonCoordinatesJson).HasColumnType("json");
            entity.HasOne(e => e.Restaurant)
                .WithMany(r => r.DeliveryZones)
                .HasForeignKey(e => e.RestaurantId);
        });

        modelBuilder.Entity<RestaurantHours>(entity =>
        {
            entity.HasKey(e => e.HoursId);
            entity.HasIndex(e => e.RestaurantId);
            entity.HasIndex(e => new { e.RestaurantId, e.DayOfWeek }).IsUnique();
            entity.HasOne(e => e.Restaurant)
                .WithMany(r => r.Hours)
                .HasForeignKey(e => e.RestaurantId);
        });
    }
}
