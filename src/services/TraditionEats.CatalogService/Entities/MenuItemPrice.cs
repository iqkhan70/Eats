namespace TraditionEats.CatalogService.Entities;

public class MenuItemPrice
{
    public Guid PriceId { get; set; }
    public Guid MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;
    public decimal Price { get; set; }
    public string? PriceType { get; set; } // "regular", "large", "combo", etc.
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }
}
