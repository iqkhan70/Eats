namespace TraditionEats.Contracts.Events;

public record DeliveryAssignedEvent(
    Guid DeliveryId,
    Guid OrderId,
    Guid DriverId,
    DateTime AssignedAt,
    int EstimatedMinutes
);

public record DriverPickedUpEvent(
    Guid DeliveryId,
    Guid OrderId,
    Guid DriverId,
    DateTime PickedUpAt,
    double? Latitude,
    double? Longitude
);

public record OrderDeliveredEvent(
    Guid DeliveryId,
    Guid OrderId,
    Guid DriverId,
    DateTime DeliveredAt,
    double? Latitude,
    double? Longitude
);
