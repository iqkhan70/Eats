using Microsoft.EntityFrameworkCore;
using TraditionEats.ReviewService.Entities;

namespace TraditionEats.ReviewService.Data;

public class ReviewDbContext : DbContext
{
    public ReviewDbContext(DbContextOptions<ReviewDbContext> options) : base(options)
    {
    }

    public DbSet<Review> Reviews { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasKey(e => e.ReviewId);
            entity.HasIndex(e => e.OrderId).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.RestaurantId);
            entity.HasIndex(e => e.Rating);
            entity.HasIndex(e => e.IsVisible);
            entity.Property(e => e.Comment).HasMaxLength(2000);
            entity.Property(e => e.Response).HasMaxLength(2000);
            entity.Property(e => e.TagsJson).HasColumnType("json");
        });
    }
}
