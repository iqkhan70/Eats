namespace TraditionalEats.Contracts.DTOs;

public record MenuItemDto(
    Guid MenuItemId,
    Guid RestaurantId,
    Guid CategoryId,
    string Name,
    string? Description,
    decimal Price,
    string? ImageUrl,
    bool IsAvailable,
    List<MenuItemOptionDto> Options,
    List<string> DietaryTags
);

public record MenuItemOptionDto(
    Guid OptionId,
    string Name,
    string Type, // "size", "spice", "addon", etc.
    List<OptionValueDto> Values
);

public record OptionValueDto(
    Guid ValueId,
    string Name,
    decimal? AdditionalPrice
);
