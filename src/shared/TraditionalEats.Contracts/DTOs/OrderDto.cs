namespace TraditionalEats.Contracts.DTOs;

public record OrderDto(
    Guid OrderId,
    Guid CustomerId,
    Guid RestaurantId,
    string RestaurantName,
    decimal TotalAmount,
    string Status,
    DateTime CreatedAt,
    DateTime? EstimatedDeliveryAt,
    List<OrderItemDto> Items,
    DeliveryAddressDto? DeliveryAddress,
    string? SpecialInstructions
);

public record OrderItemDto(
    Guid MenuItemId,
    string Name,
    string? Description,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice,
    List<string> Modifiers
);

public record DeliveryAddressDto(
    string Line1,
    string? Line2,
    string City,
    string State,
    string ZipCode,
    double? Latitude,
    double? Longitude
);
