namespace TraditionalEats.RestaurantService.Entities;

/// <summary>
/// Links a Staff user to a restaurant they can manage orders for.
/// </summary>
public class RestaurantStaff
{
    public Guid Id { get; set; }
    public Guid RestaurantId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Restaurant Restaurant { get; set; } = null!;
}
