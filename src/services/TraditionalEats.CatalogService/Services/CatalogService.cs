using Microsoft.EntityFrameworkCore;
using TraditionalEats.BuildingBlocks.Redis;
using TraditionalEats.CatalogService.Data;
using TraditionalEats.CatalogService.Entities;
using System.Text.Json;

namespace TraditionalEats.CatalogService.Services;

public interface ICatalogService
{
    Task<Guid> CreateCategoryAsync(CreateCategoryDto dto);
    Task<List<CategoryDto>> GetCategoriesAsync();
    Task<CategoryDto?> GetCategoryAsync(Guid categoryId);
    Task<bool> UpdateCategoryAsync(Guid categoryId, UpdateCategoryDto dto);
    Task<Guid> CreateMenuItemAsync(Guid restaurantId, CreateMenuItemDto dto);
    Task<MenuItemDto?> GetMenuItemAsync(Guid menuItemId);
    Task<List<MenuItemDto>> GetMenuItemsByRestaurantAsync(Guid restaurantId, Guid? categoryId = null);
    Task<bool> UpdateMenuItemAsync(Guid menuItemId, Guid restaurantId, UpdateMenuItemDto dto);
    Task<bool> SetMenuItemAvailabilityAsync(Guid menuItemId, Guid restaurantId, bool isAvailable);
    Task<Guid> AddMenuItemOptionAsync(Guid menuItemId, Guid restaurantId, CreateMenuItemOptionDto dto);
    Task<List<MenuItemOptionDto>> GetMenuItemOptionsAsync(Guid menuItemId);
    Task<Guid> AddMenuItemPriceAsync(Guid menuItemId, Guid restaurantId, CreateMenuItemPriceDto dto);
    Task<List<MenuItemPriceDto>> GetMenuItemPricesAsync(Guid menuItemId);
}

public class CatalogService : ICatalogService
{
    private readonly CatalogDbContext _context;
    private readonly IRedisService _redis;
    private readonly ILogger<CatalogService> _logger;

    public CatalogService(
        CatalogDbContext context,
        IRedisService redis,
        ILogger<CatalogService> logger)
    {
        _context = context;
        _redis = redis;
        _logger = logger;
    }

    public async Task<Guid> CreateCategoryAsync(CreateCategoryDto dto)
    {
        var categoryId = Guid.NewGuid();

        var category = new Category
        {
            CategoryId = categoryId,
            Name = dto.Name,
            Description = dto.Description,
            ImageUrl = dto.ImageUrl,
            DisplayOrder = dto.DisplayOrder,
            CreatedAt = DateTime.UtcNow
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        // Invalidate categories cache
        await _redis.DeleteAsync("categories:all");

        _logger.LogInformation("Created category {CategoryId}: {Name}", categoryId, dto.Name);
        return categoryId;
    }

    public async Task<List<CategoryDto>> GetCategoriesAsync()
    {
        // Try cache first
        var cached = await _redis.GetAsync<List<CategoryDto>>("categories:all");
        if (cached != null)
        {
            return cached;
        }

        var categories = await _context.Categories
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();

        var dtos = categories.Select(MapToDto).ToList();

        // Cache for 1 hour
        await _redis.SetAsync("categories:all", dtos, TimeSpan.FromHours(1));

        return dtos;
    }

    public async Task<CategoryDto?> GetCategoryAsync(Guid categoryId)
    {
        var category = await _context.Categories
            .Include(c => c.MenuItems)
            .FirstOrDefaultAsync(c => c.CategoryId == categoryId);

        return category == null ? null : MapToDto(category);
    }

    public async Task<bool> UpdateCategoryAsync(Guid categoryId, UpdateCategoryDto dto)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.CategoryId == categoryId);

        if (category == null)
        {
            return false;
        }

        if (dto.Name != null) category.Name = dto.Name;
        if (dto.Description != null) category.Description = dto.Description;
        if (dto.ImageUrl != null) category.ImageUrl = dto.ImageUrl;
        if (dto.DisplayOrder.HasValue) category.DisplayOrder = dto.DisplayOrder.Value;

        await _context.SaveChangesAsync();

        // Invalidate cache
        await _redis.DeleteAsync("categories:all");
        await _redis.DeleteAsync($"category:{categoryId}");

        return true;
    }

    public async Task<Guid> CreateMenuItemAsync(Guid restaurantId, CreateMenuItemDto dto)
    {
        var menuItemId = Guid.NewGuid();

        var menuItem = new MenuItem
        {
            MenuItemId = menuItemId,
            RestaurantId = restaurantId,
            CategoryId = dto.CategoryId,
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            ImageUrl = dto.ImageUrl,
            IsAvailable = true,
            DietaryTagsJson = JsonSerializer.Serialize(dto.DietaryTags ?? new List<string>()),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.MenuItems.Add(menuItem);
        await _context.SaveChangesAsync();

        // Invalidate restaurant menu cache
        await _redis.DeleteAsync($"menu:restaurant:{restaurantId}");

        _logger.LogInformation("Created menu item {MenuItemId} for restaurant {RestaurantId}", menuItemId, restaurantId);
        return menuItemId;
    }

    public async Task<MenuItemDto?> GetMenuItemAsync(Guid menuItemId)
    {
        // Try cache first
        var cached = await _redis.GetAsync<MenuItemDto>($"menu:item:{menuItemId}");
        if (cached != null)
        {
            return cached;
        }

        var menuItem = await _context.MenuItems
            .Include(m => m.Category)
            .Include(m => m.Options)
            .Include(m => m.Prices)
            .FirstOrDefaultAsync(m => m.MenuItemId == menuItemId);

        if (menuItem == null)
        {
            return null;
        }

        var dto = MapToDto(menuItem);

        // Cache for 30 minutes
        await _redis.SetAsync($"menu:item:{menuItemId}", dto, TimeSpan.FromMinutes(30));

        return dto;
    }

    public async Task<List<MenuItemDto>> GetMenuItemsByRestaurantAsync(Guid restaurantId, Guid? categoryId = null)
    {
        var cacheKey = categoryId.HasValue 
            ? $"menu:restaurant:{restaurantId}:category:{categoryId}"
            : $"menu:restaurant:{restaurantId}";

        // Try cache first
        var cached = await _redis.GetAsync<List<MenuItemDto>>(cacheKey);
        if (cached != null)
        {
            return cached;
        }

        var query = _context.MenuItems
            .Where(m => m.RestaurantId == restaurantId)
            .Include(m => m.Category)
            .Include(m => m.Options)
            .Include(m => m.Prices)
            .AsQueryable();

        if (categoryId.HasValue)
        {
            query = query.Where(m => m.CategoryId == categoryId.Value);
        }

        var menuItems = await query
            .OrderBy(m => m.Category!.DisplayOrder)
            .ThenBy(m => m.Name)
            .ToListAsync();

        var dtos = menuItems.Select(MapToDto).ToList();

        // Cache for 15 minutes
        await _redis.SetAsync(cacheKey, dtos, TimeSpan.FromMinutes(15));

        return dtos;
    }

    public async Task<bool> UpdateMenuItemAsync(Guid menuItemId, Guid restaurantId, UpdateMenuItemDto dto)
    {
        var menuItem = await _context.MenuItems
            .FirstOrDefaultAsync(m => m.MenuItemId == menuItemId && m.RestaurantId == restaurantId);

        if (menuItem == null)
        {
            return false;
        }

        if (dto.Name != null) menuItem.Name = dto.Name;
        if (dto.Description != null) menuItem.Description = dto.Description;
        if (dto.Price.HasValue) menuItem.Price = dto.Price.Value;
        if (dto.ImageUrl != null) menuItem.ImageUrl = dto.ImageUrl;
        if (dto.CategoryId.HasValue) menuItem.CategoryId = dto.CategoryId.Value;
        if (dto.DietaryTags != null) menuItem.DietaryTagsJson = JsonSerializer.Serialize(dto.DietaryTags);

        menuItem.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Invalidate caches
        await _redis.DeleteAsync($"menu:item:{menuItemId}");
        await _redis.DeleteAsync($"menu:restaurant:{restaurantId}");

        return true;
    }

    public async Task<bool> SetMenuItemAvailabilityAsync(Guid menuItemId, Guid restaurantId, bool isAvailable)
    {
        var menuItem = await _context.MenuItems
            .FirstOrDefaultAsync(m => m.MenuItemId == menuItemId && m.RestaurantId == restaurantId);

        if (menuItem == null)
        {
            return false;
        }

        menuItem.IsAvailable = isAvailable;
        menuItem.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Invalidate caches
        await _redis.DeleteAsync($"menu:item:{menuItemId}");
        await _redis.DeleteAsync($"menu:restaurant:{restaurantId}");

        return true;
    }

    public async Task<Guid> AddMenuItemOptionAsync(Guid menuItemId, Guid restaurantId, CreateMenuItemOptionDto dto)
    {
        // Verify menu item belongs to restaurant
        var menuItem = await _context.MenuItems
            .FirstOrDefaultAsync(m => m.MenuItemId == menuItemId && m.RestaurantId == restaurantId);

        if (menuItem == null)
        {
            throw new UnauthorizedAccessException("Menu item not found or you don't own it");
        }

        var optionId = Guid.NewGuid();
        var option = new MenuItemOption
        {
            OptionId = optionId,
            MenuItemId = menuItemId,
            Name = dto.Name,
            Type = dto.Type,
            ValuesJson = JsonSerializer.Serialize(dto.Values)
        };

        _context.MenuItemOptions.Add(option);
        await _context.SaveChangesAsync();

        // Invalidate cache
        await _redis.DeleteAsync($"menu:item:{menuItemId}");
        await _redis.DeleteAsync($"menu:restaurant:{restaurantId}");

        return optionId;
    }

    public async Task<List<MenuItemOptionDto>> GetMenuItemOptionsAsync(Guid menuItemId)
    {
        var options = await _context.MenuItemOptions
            .Where(o => o.MenuItemId == menuItemId)
            .ToListAsync();

        return options.Select(o => new MenuItemOptionDto
        {
            OptionId = o.OptionId,
            Name = o.Name,
            Type = o.Type,
            Values = JsonSerializer.Deserialize<List<OptionValueDto>>(o.ValuesJson ?? "[]") ?? new()
        }).ToList();
    }

    public async Task<Guid> AddMenuItemPriceAsync(Guid menuItemId, Guid restaurantId, CreateMenuItemPriceDto dto)
    {
        // Verify menu item belongs to restaurant
        var menuItem = await _context.MenuItems
            .FirstOrDefaultAsync(m => m.MenuItemId == menuItemId && m.RestaurantId == restaurantId);

        if (menuItem == null)
        {
            throw new UnauthorizedAccessException("Menu item not found or you don't own it");
        }

        var priceId = Guid.NewGuid();
        var price = new MenuItemPrice
        {
            PriceId = priceId,
            MenuItemId = menuItemId,
            Price = dto.Price,
            PriceType = dto.PriceType,
            EffectiveFrom = dto.EffectiveFrom ?? DateTime.UtcNow,
            EffectiveTo = dto.EffectiveTo
        };

        _context.MenuItemPrices.Add(price);
        await _context.SaveChangesAsync();

        // Invalidate cache
        await _redis.DeleteAsync($"menu:item:{menuItemId}");
        await _redis.DeleteAsync($"menu:restaurant:{restaurantId}");

        return priceId;
    }

    public async Task<List<MenuItemPriceDto>> GetMenuItemPricesAsync(Guid menuItemId)
    {
        var now = DateTime.UtcNow;
        var prices = await _context.MenuItemPrices
            .Where(p => p.MenuItemId == menuItemId 
                && p.EffectiveFrom <= now 
                && (p.EffectiveTo == null || p.EffectiveTo >= now))
            .OrderByDescending(p => p.EffectiveFrom)
            .ToListAsync();

        return prices.Select(p => new MenuItemPriceDto
        {
            PriceId = p.PriceId,
            Price = p.Price,
            PriceType = p.PriceType,
            EffectiveFrom = p.EffectiveFrom,
            EffectiveTo = p.EffectiveTo
        }).ToList();
    }

    private CategoryDto MapToDto(Category category)
    {
        return new CategoryDto
        {
            CategoryId = category.CategoryId,
            Name = category.Name,
            Description = category.Description,
            ImageUrl = category.ImageUrl,
            DisplayOrder = category.DisplayOrder,
            CreatedAt = category.CreatedAt,
            MenuItemCount = category.MenuItems?.Count ?? 0
        };
    }

    private MenuItemDto MapToDto(MenuItem menuItem)
    {
        return new MenuItemDto
        {
            MenuItemId = menuItem.MenuItemId,
            RestaurantId = menuItem.RestaurantId,
            CategoryId = menuItem.CategoryId,
            CategoryName = menuItem.Category?.Name,
            Name = menuItem.Name,
            Description = menuItem.Description,
            Price = menuItem.Price,
            ImageUrl = menuItem.ImageUrl,
            IsAvailable = menuItem.IsAvailable,
            DietaryTags = JsonSerializer.Deserialize<List<string>>(menuItem.DietaryTagsJson ?? "[]") ?? new(),
            CreatedAt = menuItem.CreatedAt,
            UpdatedAt = menuItem.UpdatedAt,
            Options = menuItem.Options.Select(o => new MenuItemOptionDto
            {
                OptionId = o.OptionId,
                Name = o.Name,
                Type = o.Type,
                Values = JsonSerializer.Deserialize<List<OptionValueDto>>(o.ValuesJson ?? "[]") ?? new()
            }).ToList(),
            Prices = menuItem.Prices.Select(p => new MenuItemPriceDto
            {
                PriceId = p.PriceId,
                Price = p.Price,
                PriceType = p.PriceType,
                EffectiveFrom = p.EffectiveFrom,
                EffectiveTo = p.EffectiveTo
            }).ToList()
        };
    }
}

// DTOs
public record CreateCategoryDto(
    string Name,
    string? Description,
    string? ImageUrl,
    int DisplayOrder);

public record UpdateCategoryDto(
    string? Name,
    string? Description,
    string? ImageUrl,
    int? DisplayOrder);

public record CategoryDto
{
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public int MenuItemCount { get; set; }
}

public record CreateMenuItemDto(
    Guid CategoryId,
    string Name,
    string? Description,
    decimal Price,
    string? ImageUrl,
    List<string>? DietaryTags);

public record UpdateMenuItemDto(
    string? Name,
    string? Description,
    decimal? Price,
    string? ImageUrl,
    Guid? CategoryId,
    List<string>? DietaryTags);

public record MenuItemDto
{
    public Guid MenuItemId { get; set; }
    public Guid RestaurantId { get; set; }
    public Guid CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; }
    public List<string> DietaryTags { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<MenuItemOptionDto> Options { get; set; } = new();
    public List<MenuItemPriceDto> Prices { get; set; } = new();
}

public record CreateMenuItemOptionDto(
    string Name,
    string Type,
    List<OptionValueDto> Values);

public record MenuItemOptionDto
{
    public Guid OptionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public List<OptionValueDto> Values { get; set; } = new();
}

public record OptionValueDto
{
    public Guid ValueId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? AdditionalPrice { get; set; }
}

public record CreateMenuItemPriceDto(
    decimal Price,
    string? PriceType,
    DateTime? EffectiveFrom,
    DateTime? EffectiveTo);

public record MenuItemPriceDto
{
    public Guid PriceId { get; set; }
    public decimal Price { get; set; }
    public string? PriceType { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}
