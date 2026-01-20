using TraditionalEats.CatalogService.Entities;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace TraditionalEats.CatalogService.Data;

public static class SeedData
{
    private static ILogger? _logger;

    public static void SetLogger(ILogger logger)
    {
        _logger = logger;
    }

    public static async Task SeedAsync(CatalogDbContext context, IHttpClientFactory? httpClientFactory = null)
    {
        // Seed categories first
        await SeedCategoriesAsync(context);

        // Seed menu items for restaurants
        // We'll get restaurant IDs from RestaurantService via HTTP
        if (httpClientFactory != null)
        {
            await SeedMenuItemsForRestaurantsAsync(context, httpClientFactory);
        }
    }

    private static async Task SeedCategoriesAsync(CatalogDbContext context)
    {
        // Check if categories already exist
        if (await context.Categories.AnyAsync())
        {
            return; // Categories already seeded
        }

        var now = DateTime.UtcNow;

        // Create categories
        var appetizers = new Category
        {
            CategoryId = Guid.NewGuid(),
            Name = "Appetizers",
            Description = "Start your meal with our delicious appetizers",
            DisplayOrder = 1,
            CreatedAt = now
        };

        var mains = new Category
        {
            CategoryId = Guid.NewGuid(),
            Name = "Main Courses",
            Description = "Hearty main dishes",
            DisplayOrder = 2,
            CreatedAt = now
        };

        var desserts = new Category
        {
            CategoryId = Guid.NewGuid(),
            Name = "Desserts",
            Description = "Sweet treats to end your meal",
            DisplayOrder = 3,
            CreatedAt = now
        };

        var beverages = new Category
        {
            CategoryId = Guid.NewGuid(),
            Name = "Beverages",
            Description = "Refreshing drinks",
            DisplayOrder = 4,
            CreatedAt = now
        };

        var sides = new Category
        {
            CategoryId = Guid.NewGuid(),
            Name = "Sides",
            Description = "Perfect accompaniments",
            DisplayOrder = 5,
            CreatedAt = now
        };

        context.Categories.AddRange(appetizers, mains, desserts, beverages, sides);
        await context.SaveChangesAsync();

        _logger?.LogInformation("Seeded {Count} categories", 5);
    }

    private static async Task SeedMenuItemsForRestaurantsAsync(
        CatalogDbContext context,
        IHttpClientFactory httpClientFactory)
    {
        try
        {
            // Get restaurants from RestaurantService
            var client = httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("http://localhost:5007"); // RestaurantService port
            
            var response = await client.GetAsync("/api/restaurant");
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Could not fetch restaurants for menu seeding. RestaurantService may not be running.");
                return;
            }

            var restaurantsJson = await response.Content.ReadAsStringAsync();
            var restaurants = System.Text.Json.JsonSerializer.Deserialize<List<RestaurantInfo>>(
                restaurantsJson,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (restaurants == null || !restaurants.Any())
            {
                _logger?.LogWarning("No restaurants found for menu seeding");
                return;
            }

            // Get categories
            var categories = await context.Categories.OrderBy(c => c.DisplayOrder).ToListAsync();
            if (!categories.Any())
            {
                _logger?.LogWarning("No categories found. Seed categories first.");
                return;
            }

            var appetizers = categories.FirstOrDefault(c => c.Name == "Appetizers");
            var mains = categories.FirstOrDefault(c => c.Name == "Main Courses");
            var desserts = categories.FirstOrDefault(c => c.Name == "Desserts");
            var beverages = categories.FirstOrDefault(c => c.Name == "Beverages");
            var sides = categories.FirstOrDefault(c => c.Name == "Sides");

            if (appetizers == null || mains == null || desserts == null || beverages == null || sides == null)
            {
                _logger?.LogWarning("Required categories not found");
                return;
            }

            // Seed menu items for each restaurant (limit to first 3 for demo)
            foreach (var restaurant in restaurants.Take(3))
            {
                await SeedMenuItemsForRestaurantAsync(
                    context,
                    Guid.Parse(restaurant.RestaurantId),
                    appetizers.CategoryId,
                    mains.CategoryId,
                    desserts.CategoryId,
                    beverages.CategoryId,
                    sides.CategoryId);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error seeding menu items from RestaurantService");
        }
    }

    private static async Task SeedMenuItemsForRestaurantAsync(
        CatalogDbContext context,
        Guid restaurantId,
        Guid appetizersId,
        Guid mainsId,
        Guid dessertsId,
        Guid beveragesId,
        Guid sidesId)
    {
        // Check if menu items already exist for this restaurant
        if (await context.MenuItems.AnyAsync(m => m.RestaurantId == restaurantId))
        {
            return;
        }

        var now = DateTime.UtcNow;

        var menuItems = new List<MenuItem>
        {
            // Appetizers
            new MenuItem
            {
                MenuItemId = Guid.NewGuid(),
                RestaurantId = restaurantId,
                CategoryId = appetizersId,
                Name = "Traditional Soup",
                Description = "Hearty homemade soup with fresh vegetables",
                Price = 8.99m,
                ImageUrl = "https://example.com/images/soup.jpg",
                IsAvailable = true,
                DietaryTagsJson = JsonSerializer.Serialize(new List<string> { "Vegetarian" }),
                CreatedAt = now,
                UpdatedAt = now
            },
            new MenuItem
            {
                MenuItemId = Guid.NewGuid(),
                RestaurantId = restaurantId,
                CategoryId = appetizersId,
                Name = "Bread Basket",
                Description = "Fresh baked bread with butter",
                Price = 5.99m,
                ImageUrl = "https://example.com/images/bread.jpg",
                IsAvailable = true,
                DietaryTagsJson = JsonSerializer.Serialize(new List<string> { "Vegetarian" }),
                CreatedAt = now,
                UpdatedAt = now
            },
            new MenuItem
            {
                MenuItemId = Guid.NewGuid(),
                RestaurantId = restaurantId,
                CategoryId = appetizersId,
                Name = "Stuffed Peppers",
                Description = "Bell peppers stuffed with rice and herbs",
                Price = 9.99m,
                ImageUrl = "https://example.com/images/peppers.jpg",
                IsAvailable = true,
                DietaryTagsJson = JsonSerializer.Serialize(new List<string> { "Vegetarian" }),
                CreatedAt = now,
                UpdatedAt = now
            },

            // Main Courses
            new MenuItem
            {
                MenuItemId = Guid.NewGuid(),
                RestaurantId = restaurantId,
                CategoryId = mainsId,
                Name = "Traditional Roast",
                Description = "Slow-roasted meat with vegetables and gravy",
                Price = 24.99m,
                ImageUrl = "https://example.com/images/roast.jpg",
                IsAvailable = true,
                DietaryTagsJson = JsonSerializer.Serialize(new List<string>()),
                CreatedAt = now,
                UpdatedAt = now
            },
            new MenuItem
            {
                MenuItemId = Guid.NewGuid(),
                RestaurantId = restaurantId,
                CategoryId = mainsId,
                Name = "Heritage Stew",
                Description = "Rich stew with tender meat and vegetables",
                Price = 18.99m,
                ImageUrl = "https://example.com/images/stew.jpg",
                IsAvailable = true,
                DietaryTagsJson = JsonSerializer.Serialize(new List<string>()),
                CreatedAt = now,
                UpdatedAt = now
            },
            new MenuItem
            {
                MenuItemId = Guid.NewGuid(),
                RestaurantId = restaurantId,
                CategoryId = mainsId,
                Name = "Grilled Fish",
                Description = "Fresh fish grilled to perfection",
                Price = 22.99m,
                ImageUrl = "https://example.com/images/fish.jpg",
                IsAvailable = true,
                DietaryTagsJson = JsonSerializer.Serialize(new List<string> { "Gluten-Free" }),
                CreatedAt = now,
                UpdatedAt = now
            },
            new MenuItem
            {
                MenuItemId = Guid.NewGuid(),
                RestaurantId = restaurantId,
                CategoryId = mainsId,
                Name = "Vegetarian Platter",
                Description = "Assorted vegetarian dishes",
                Price = 16.99m,
                ImageUrl = "https://example.com/images/vegetarian.jpg",
                IsAvailable = true,
                DietaryTagsJson = JsonSerializer.Serialize(new List<string> { "Vegetarian", "Vegan" }),
                CreatedAt = now,
                UpdatedAt = now
            },

            // Desserts
            new MenuItem
            {
                MenuItemId = Guid.NewGuid(),
                RestaurantId = restaurantId,
                CategoryId = dessertsId,
                Name = "Traditional Cake",
                Description = "Homemade cake with cream frosting",
                Price = 7.99m,
                ImageUrl = "https://example.com/images/cake.jpg",
                IsAvailable = true,
                DietaryTagsJson = JsonSerializer.Serialize(new List<string>()),
                CreatedAt = now,
                UpdatedAt = now
            },
            new MenuItem
            {
                MenuItemId = Guid.NewGuid(),
                RestaurantId = restaurantId,
                CategoryId = dessertsId,
                Name = "Fruit Pie",
                Description = "Fresh fruit pie with flaky crust",
                Price = 6.99m,
                ImageUrl = "https://example.com/images/pie.jpg",
                IsAvailable = true,
                DietaryTagsJson = JsonSerializer.Serialize(new List<string> { "Vegetarian" }),
                CreatedAt = now,
                UpdatedAt = now
            },

            // Beverages
            new MenuItem
            {
                MenuItemId = Guid.NewGuid(),
                RestaurantId = restaurantId,
                CategoryId = beveragesId,
                Name = "Fresh Juice",
                Description = "Freshly squeezed fruit juice",
                Price = 4.99m,
                ImageUrl = "https://example.com/images/juice.jpg",
                IsAvailable = true,
                DietaryTagsJson = JsonSerializer.Serialize(new List<string> { "Vegetarian", "Vegan", "Gluten-Free" }),
                CreatedAt = now,
                UpdatedAt = now
            },
            new MenuItem
            {
                MenuItemId = Guid.NewGuid(),
                RestaurantId = restaurantId,
                CategoryId = beveragesId,
                Name = "Traditional Tea",
                Description = "Hot tea with herbs",
                Price = 3.99m,
                ImageUrl = "https://example.com/images/tea.jpg",
                IsAvailable = true,
                DietaryTagsJson = JsonSerializer.Serialize(new List<string> { "Vegetarian", "Vegan", "Gluten-Free" }),
                CreatedAt = now,
                UpdatedAt = now
            },

            // Sides
            new MenuItem
            {
                MenuItemId = Guid.NewGuid(),
                RestaurantId = restaurantId,
                CategoryId = sidesId,
                Name = "Roasted Vegetables",
                Description = "Seasonal vegetables roasted with herbs",
                Price = 6.99m,
                ImageUrl = "https://example.com/images/vegetables.jpg",
                IsAvailable = true,
                DietaryTagsJson = JsonSerializer.Serialize(new List<string> { "Vegetarian", "Vegan", "Gluten-Free" }),
                CreatedAt = now,
                UpdatedAt = now
            },
            new MenuItem
            {
                MenuItemId = Guid.NewGuid(),
                RestaurantId = restaurantId,
                CategoryId = sidesId,
                Name = "Mashed Potatoes",
                Description = "Creamy mashed potatoes",
                Price = 5.99m,
                ImageUrl = "https://example.com/images/potatoes.jpg",
                IsAvailable = true,
                DietaryTagsJson = JsonSerializer.Serialize(new List<string> { "Vegetarian", "Gluten-Free" }),
                CreatedAt = now,
                UpdatedAt = now
            }
        };

        context.MenuItems.AddRange(menuItems);
        await context.SaveChangesAsync();

        _logger?.LogInformation("Seeded {Count} menu items for restaurant {RestaurantId}", menuItems.Count, restaurantId);
    }

    private class RestaurantInfo
    {
        public string RestaurantId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
