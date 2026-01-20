using Microsoft.EntityFrameworkCore;
using TraditionEats.PromotionService.Entities;

namespace TraditionEats.PromotionService.Data;

public class PromotionDbContext : DbContext
{
    public PromotionDbContext(DbContextOptions<PromotionDbContext> options) : base(options)
    {
    }

    public DbSet<Promotion> Promotions { get; set; }
    public DbSet<PromotionUsage> PromotionUsages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Promotion>(entity =>
        {
            entity.HasKey(e => e.PromotionId);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.RestaurantId);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => new { e.StartDate, e.EndDate });
            entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
        });

        modelBuilder.Entity<PromotionUsage>(entity =>
        {
            entity.HasKey(e => e.UsageId);
            entity.HasIndex(e => e.PromotionId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.OrderId);
            entity.HasOne(e => e.Promotion)
                .WithMany()
                .HasForeignKey(e => e.PromotionId);
        });
    }
}
