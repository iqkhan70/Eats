using Microsoft.EntityFrameworkCore;
using TraditionalEats.OrderService.Entities;

namespace TraditionalEats.OrderService.Data;

public class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
    {
    }

    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<OrderStatusHistory> OrderStatusHistory { get; set; }
    public DbSet<OrderHistory> OrderHistory { get; set; }
    public DbSet<OrderItemHistory> OrderItemHistory { get; set; }
    public DbSet<OrderStatusHistoryArchive> OrderStatusHistoryArchive { get; set; }
    public DbSet<Cart> Carts { get; set; }
    public DbSet<CartItem> CartItems { get; set; }
    public DbSet<OrderIdempotencyKey> OrderIdempotencyKeys { get; set; }
    public DbSet<AdminCleanupAuditLog> AdminCleanupAuditLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderId);
            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.RestaurantId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.IdempotencyKey);
            entity.Property(e => e.Status).HasMaxLength(50);
            
            // Configure relationship with OrderStatusHistory
            entity.HasMany(o => o.StatusHistory)
                .WithOne(h => h.Order)
                .HasForeignKey(h => h.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.OrderItemId);
            entity.HasIndex(e => e.OrderId);
        });

        modelBuilder.Entity<OrderStatusHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OrderId);
            entity.Property(e => e.Status).HasMaxLength(50);
        });

        modelBuilder.Entity<OrderHistory>(entity =>
        {
            entity.ToTable("order_history");
            entity.HasKey(e => e.OrderId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.ArchivedAt);
            entity.Property(e => e.Status).HasMaxLength(50);
        });

        modelBuilder.Entity<OrderItemHistory>(entity =>
        {
            entity.ToTable("order_item_history");
            entity.HasKey(e => e.OrderItemId);
            entity.HasIndex(e => e.OrderId);
        });

        modelBuilder.Entity<OrderStatusHistoryArchive>(entity =>
        {
            entity.ToTable("order_status_history_archive");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OrderId);
            entity.Property(e => e.Status).HasMaxLength(50);
        });

        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasKey(e => e.CartId);
            entity.HasIndex(e => e.CustomerId);
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasKey(e => e.CartItemId);
            entity.HasIndex(e => e.CartId);
            // Add unique constraint on CartId + MenuItemId to prevent duplicate items
            entity.HasIndex(e => new { e.CartId, e.MenuItemId })
                .IsUnique();
        });

        modelBuilder.Entity<OrderIdempotencyKey>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Key).IsUnique();
            entity.HasIndex(e => e.OrderId);
        });

        modelBuilder.Entity<AdminCleanupAuditLog>(entity =>
        {
            entity.ToTable("admin_cleanup_audit_log");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.RanAt);
            entity.HasIndex(e => e.JobType);
            entity.Property(e => e.ParametersJson).HasMaxLength(512);
            entity.Property(e => e.ResultJson).HasMaxLength(2048);
            entity.Property(e => e.RanByUserId).HasMaxLength(128);
            entity.Property(e => e.RanByEmail).HasMaxLength(256);
            entity.Property(e => e.Status).HasMaxLength(32);
            entity.Property(e => e.ErrorMessage).HasMaxLength(1024);
        });
    }
}
