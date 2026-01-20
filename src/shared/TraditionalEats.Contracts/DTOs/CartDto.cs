namespace TraditionalEats.Contracts.DTOs;

public record CartDto(
    Guid CartId,
    Guid? CustomerId,
    Guid? RestaurantId,
    List<CartItemDto> Items,
    decimal Subtotal,
    decimal Tax,
    decimal DeliveryFee,
    decimal Total,
    DateTime UpdatedAt
);

public record CartItemDto(
    Guid CartItemId,
    Guid MenuItemId,
    string Name,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice,
    Dictionary<string, string> SelectedOptions
);
