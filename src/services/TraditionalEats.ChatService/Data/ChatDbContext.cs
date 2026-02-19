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
    public DbSet<VendorConversation> VendorConversations { get; set; }
    public DbSet<VendorChatMessage> VendorMessages { get; set; }

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
            entity.Property(e => e.SenderDisplayName).HasColumnName("sender_display_name").HasMaxLength(256);
            entity.Property(e => e.Message).HasColumnName("message").HasColumnType("TEXT").IsRequired();
            entity.Property(e => e.SentAt).HasColumnName("sent_at").IsRequired();
            entity.Property(e => e.IsRead).HasColumnName("is_read").HasDefaultValue(false);
            entity.Property(e => e.ReadAt).HasColumnName("read_at");
            entity.Property(e => e.MetadataJson).HasColumnName("metadata_json").HasColumnType("JSON");

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

        // VendorConversation configuration (generic vendor/customer chat)
        modelBuilder.Entity<VendorConversation>(entity =>
        {
            entity.ToTable("vendor_conversations");
            entity.HasKey(e => e.ConversationId);
            entity.Property(e => e.ConversationId).HasColumnName("conversation_id");
            entity.Property(e => e.RestaurantId).HasColumnName("restaurant_id").IsRequired();
            entity.Property(e => e.CustomerId).HasColumnName("customer_id").IsRequired();
            entity.Property(e => e.CustomerDisplayName).HasColumnName("customer_display_name").HasMaxLength(256);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.Property(e => e.LastMessageAt).HasColumnName("last_message_at");

            entity.HasIndex(e => e.RestaurantId);
            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => new { e.RestaurantId, e.CustomerId }).IsUnique();
            entity.HasIndex(e => e.LastMessageAt);
        });

        modelBuilder.Entity<VendorChatMessage>(entity =>
        {
            entity.ToTable("vendor_chat_messages");
            entity.HasKey(e => e.MessageId);
            entity.Property(e => e.MessageId).HasColumnName("message_id");
            entity.Property(e => e.ConversationId).HasColumnName("conversation_id").IsRequired();
            entity.Property(e => e.SenderId).HasColumnName("sender_id").IsRequired();
            entity.Property(e => e.SenderRole).HasColumnName("sender_role").HasMaxLength(50).IsRequired();
            entity.Property(e => e.SenderDisplayName).HasColumnName("sender_display_name").HasMaxLength(256);
            entity.Property(e => e.Message).HasColumnName("message").HasColumnType("TEXT").IsRequired();
            entity.Property(e => e.SentAt).HasColumnName("sent_at").IsRequired();
            entity.Property(e => e.IsRead).HasColumnName("is_read").HasDefaultValue(false);
            entity.Property(e => e.ReadAt).HasColumnName("read_at");
            entity.Property(e => e.MetadataJson).HasColumnName("metadata_json").HasColumnType("JSON");

            entity.HasIndex(e => e.ConversationId);
            entity.HasIndex(e => e.SenderId);
            entity.HasIndex(e => e.SentAt);
        });
    }
}
