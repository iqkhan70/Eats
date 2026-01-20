using Microsoft.EntityFrameworkCore;
using TraditionEats.CatalogService.Entities;

namespace TraditionEats.CatalogService.Data;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
    {
    }

    public DbSet<Category> Categories { get; set; }
    public DbSet<MenuItem> MenuItems { get; set; }
    public DbSet<MenuItemOption> MenuItemOptions { get; set; }
    public DbSet<MenuItemPrice> MenuItemPrices { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.CategoryId);
            entity.HasIndex(e => e.Name);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Description).HasMaxLength(1000);
        });

        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.HasKey(e => e.MenuItemId);
            entity.HasIndex(e => e.RestaurantId);
            entity.HasIndex(e => e.CategoryId);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.HasOne(e => e.Category)
                .WithMany(c => c.MenuItems)
                .HasForeignKey(e => e.CategoryId);
            entity.Property(e => e.DietaryTagsJson).HasColumnType("json");
        });

        modelBuilder.Entity<MenuItemOption>(entity =>
        {
            entity.HasKey(e => e.OptionId);
            entity.HasIndex(e => e.MenuItemId);
            entity.HasOne(e => e.MenuItem)
                .WithMany(m => m.Options)
                .HasForeignKey(e => e.MenuItemId);
            entity.Property(e => e.ValuesJson).HasColumnType("json");
        });

        modelBuilder.Entity<MenuItemPrice>(entity =>
        {
            entity.HasKey(e => e.PriceId);
            entity.HasIndex(e => e.MenuItemId);
            entity.HasOne(e => e.MenuItem)
                .WithMany(m => m.Prices)
                .HasForeignKey(e => e.MenuItemId);
        });
    }
}
