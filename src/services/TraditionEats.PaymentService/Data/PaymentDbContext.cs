using Microsoft.EntityFrameworkCore;
using TraditionEats.PaymentService.Entities;

namespace TraditionEats.PaymentService.Data;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options)
    {
    }

    public DbSet<PaymentIntent> PaymentIntents { get; set; }
    public DbSet<Refund> Refunds { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PaymentIntent>(entity =>
        {
            entity.HasKey(e => e.PaymentIntentId);
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.ProviderIntentId);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.Provider).HasMaxLength(50);
        });

        modelBuilder.Entity<Refund>(entity =>
        {
            entity.HasKey(e => e.RefundId);
            entity.HasIndex(e => e.PaymentIntentId);
            entity.HasIndex(e => e.OrderId);
            entity.Property(e => e.Status).HasMaxLength(50);
        });
    }
}
