namespace TraditionEats.CatalogService.Entities;

public class MenuItem
{
    public Guid MenuItemId { get; set; }
    public Guid RestaurantId { get; set; }
    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; } = true;
    public string DietaryTagsJson { get; set; } = "[]"; // JSON array stored as string
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<MenuItemOption> Options { get; set; } = new();
    public List<MenuItemPrice> Prices { get; set; } = new();
}
