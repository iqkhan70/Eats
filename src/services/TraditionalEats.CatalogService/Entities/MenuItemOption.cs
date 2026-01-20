namespace TraditionalEats.CatalogService.Entities;

public class MenuItemOption
{
    public Guid OptionId { get; set; }
    public Guid MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "size", "spice", "addon", etc.
    public string ValuesJson { get; set; } = "[]"; // JSON array stored as string
}

public class OptionValue
{
    public Guid ValueId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? AdditionalPrice { get; set; }
}
