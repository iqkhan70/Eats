using Microsoft.EntityFrameworkCore;
using TraditionalEats.NotificationService.Entities;

namespace TraditionalEats.NotificationService.Data;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
    {
    }

    public DbSet<NotificationTemplate> Templates { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<NotificationPreference> Preferences { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<NotificationTemplate>(entity =>
        {
            entity.HasKey(e => e.TemplateId);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.Type);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Subject).HasMaxLength(500);
            entity.Property(e => e.Body).HasColumnType("text");
            entity.Property(e => e.VariablesJson).HasColumnType("json");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.Property(e => e.Channel).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Subject).HasMaxLength(500);
            entity.Property(e => e.Body).HasColumnType("text");
            entity.Property(e => e.Recipient).HasMaxLength(500);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
        });

        modelBuilder.Entity<NotificationPreference>(entity =>
        {
            entity.HasKey(e => e.PreferenceId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.Channel, e.NotificationType }).IsUnique();
            entity.Property(e => e.Channel).IsRequired().HasMaxLength(50);
            entity.Property(e => e.NotificationType).IsRequired().HasMaxLength(100);
        });
    }
}
