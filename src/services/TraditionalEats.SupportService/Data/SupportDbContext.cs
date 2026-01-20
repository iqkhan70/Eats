using Microsoft.EntityFrameworkCore;
using TraditionalEats.SupportService.Entities;

namespace TraditionalEats.SupportService.Data;

public class SupportDbContext : DbContext
{
    public SupportDbContext(DbContextOptions<SupportDbContext> options) : base(options)
    {
    }

    public DbSet<SupportTicket> Tickets { get; set; }
    public DbSet<SupportMessage> Messages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SupportTicket>(entity =>
        {
            entity.HasKey(e => e.TicketId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.AssignedTo);
            entity.Property(e => e.Subject).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Description).HasColumnType("text");
            entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Priority).IsRequired().HasMaxLength(50);
        });

        modelBuilder.Entity<SupportMessage>(entity =>
        {
            entity.HasKey(e => e.MessageId);
            entity.HasIndex(e => e.TicketId);
            entity.HasIndex(e => e.CreatedAt);
            entity.Property(e => e.Content).HasColumnType("text");
            entity.Property(e => e.AttachmentsJson).HasColumnType("json");
            entity.HasOne(e => e.Ticket)
                .WithMany(t => t.Messages)
                .HasForeignKey(e => e.TicketId);
        });
    }
}
