namespace TraditionalEats.Contracts.Events;

public record OrderPlacedEvent(
    Guid OrderId,
    Guid CustomerId,
    Guid RestaurantId,
    decimal TotalAmount,
    decimal ServiceFee,
    DateTime PlacedAt,
    string DeliveryAddress,
    List<OrderItemDto> Items
);

public record OrderItemDto(
    Guid MenuItemId,
    string Name,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice,
    List<string> Modifiers
);
