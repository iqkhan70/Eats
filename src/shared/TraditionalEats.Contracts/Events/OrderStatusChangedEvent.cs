namespace TraditionalEats.Contracts.Events;

public record OrderStatusChangedEvent(
    Guid OrderId,
    Guid CustomerId,
    Guid RestaurantId,
    string OldStatus,
    string NewStatus,
    string? Notes,
    DateTime ChangedAt
);
