using Microsoft.EntityFrameworkCore;
using TraditionalEats.ChatService.Entities;

namespace TraditionalEats.ChatService.Data;

public class ChatDbContext : DbContext
{
    public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options)
    {
    }

    public DbSet<ChatMessage> Messages { get; set; }
    public DbSet<ChatParticipant> Participants { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ChatMessage configuration
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("chat_messages");
            entity.HasKey(e => e.MessageId);
            entity.Property(e => e.MessageId).HasColumnName("message_id");
            entity.Property(e => e.OrderId).HasColumnName("order_id").IsRequired();
            entity.Property(e => e.SenderId).HasColumnName("sender_id").IsRequired();
            entity.Property(e => e.SenderRole).HasColumnName("sender_role").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Message).HasColumnName("message").HasColumnType("TEXT").IsRequired();
            entity.Property(e => e.SentAt).HasColumnName("sent_at").IsRequired();
            entity.Property(e => e.IsRead).HasColumnName("is_read").HasDefaultValue(false);
            entity.Property(e => e.ReadAt).HasColumnName("read_at");

            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.SenderId);
            entity.HasIndex(e => e.SentAt);
        });

        // ChatParticipant configuration
        modelBuilder.Entity<ChatParticipant>(entity =>
        {
            entity.ToTable("chat_participants");
            entity.HasKey(e => e.ParticipantId);
            entity.Property(e => e.ParticipantId).HasColumnName("participant_id");
            entity.Property(e => e.OrderId).HasColumnName("order_id").IsRequired();
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.Role).HasColumnName("role").HasMaxLength(50).IsRequired();
            entity.Property(e => e.JoinedAt).HasColumnName("joined_at").IsRequired();
            entity.Property(e => e.LastReadAt).HasColumnName("last_read_at");

            entity.HasIndex(e => new { e.OrderId, e.UserId }).IsUnique();
            entity.HasIndex(e => e.OrderId);
        });
    }
}
