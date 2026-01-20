namespace TraditionEats.Contracts.Events;

public record RestaurantAcceptedOrderEvent(
    Guid OrderId,
    Guid RestaurantId,
    DateTime AcceptedAt,
    int EstimatedPrepTimeMinutes
);

public record RestaurantRejectedOrderEvent(
    Guid OrderId,
    Guid RestaurantId,
    string Reason,
    DateTime RejectedAt
);
