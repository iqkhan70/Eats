namespace TraditionEats.Contracts.DTOs;

public record RestaurantDto(
    Guid RestaurantId,
    string Name,
    string? Description,
    string CuisineType,
    string PriceTier,
    string Status,
    double? Rating,
    int? ReviewCount,
    string? ImageUrl,
    bool IsOpen,
    int? EstimatedDeliveryMinutes,
    double? DistanceKm,
    RestaurantHoursDto? Hours
);

public record RestaurantHoursDto(
    Dictionary<DayOfWeek, TimeSpan?> OpenTimes,
    Dictionary<DayOfWeek, TimeSpan?> CloseTimes
);
