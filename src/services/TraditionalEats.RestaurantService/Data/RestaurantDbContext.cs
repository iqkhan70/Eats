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
    public DbSet<ZipCodeLookup> ZipCodeLookups { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.HasKey(e => e.RestaurantId);
            entity.HasIndex(e => e.OwnerId);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => new { e.Latitude, e.Longitude }); // Bounding-box / distance queries
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.CuisineType).HasMaxLength(100);
            entity.Property(e => e.PhoneNumber).HasMaxLength(50);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.EloRating).HasDefaultValue(1500m);
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

        modelBuilder.Entity<ZipCodeLookup>(entity =>
        {
            entity.ToTable("ZipCodeLookup");
            entity.HasKey(e => e.ZipCode);
            entity.Property(e => e.ZipCode).IsRequired().HasMaxLength(10);
            entity.Property(e => e.Latitude).IsRequired().HasColumnType("DECIMAL(10, 8)");
            entity.Property(e => e.Longitude).IsRequired().HasColumnType("DECIMAL(11, 8)");
            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.State).HasMaxLength(2);
            entity.HasIndex(e => e.State);
        });
    }
}
