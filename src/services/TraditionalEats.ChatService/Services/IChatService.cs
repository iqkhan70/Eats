using TraditionalEats.ChatService.Entities;

namespace TraditionalEats.ChatService.Services;

public interface IChatService
{
    Task<bool> VerifyOrderAccessAsync(Guid orderId, Guid userId, string userRole);
    Task<ChatMessage> SaveMessageAsync(Guid orderId, Guid senderId, string senderRole, string message);
    Task<List<ChatMessage>> GetOrderMessagesAsync(Guid orderId, Guid userId, string userRole);
    Task EnsureParticipantAsync(Guid orderId, Guid userId, string role);
    Task MarkMessagesAsReadAsync(Guid orderId, Guid userId);
    Task UpdateLastReadAsync(Guid orderId, Guid userId);
    Task<int> GetUnreadMessageCountAsync(Guid orderId, Guid userId);
}
