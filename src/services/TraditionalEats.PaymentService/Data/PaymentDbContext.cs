using Microsoft.EntityFrameworkCore;
using TraditionalEats.PaymentService.Entities;

namespace TraditionalEats.PaymentService.Data;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options)
    {
    }

    public DbSet<PaymentIntent> PaymentIntents { get; set; }
    public DbSet<Refund> Refunds { get; set; }
    public DbSet<Vendor> Vendors { get; set; }

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

        modelBuilder.Entity<Vendor>(entity =>
        {
            entity.HasKey(e => e.VendorId);
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.HasIndex(e => e.StripeAccountId);
            entity.Property(e => e.StripeAccountId).HasMaxLength(255);
            entity.Property(e => e.StripeOnboardingStatus).HasMaxLength(50);
        });
    }
}
