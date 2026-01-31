using TraditionalEats.RestaurantService.Entities;

namespace TraditionalEats.RestaurantService.Data;

public static class SeedData
{
    public static async Task SeedAsync(RestaurantDbContext context)
    {
        // Seed ZipCodeLookup (same data as mental health app) if table exists and is empty
        try
        {
            if (!context.ZipCodeLookups.Any())
            {
                var zipNow = DateTime.UtcNow;
                var zipCodes = new List<ZipCodeLookup>
                {
                    new() { ZipCode = "66221", Latitude = 38.97170000m, Longitude = -94.70140000m, City = "Overland Park", State = "KS", CreatedAt = zipNow },
                    new() { ZipCode = "66062", Latitude = 38.88330000m, Longitude = -94.81670000m, City = "Olathe", State = "KS", CreatedAt = zipNow },
                    new() { ZipCode = "64138", Latitude = 38.95860000m, Longitude = -94.52380000m, City = "Kansas City", State = "MO", CreatedAt = zipNow },
                    new() { ZipCode = "64110", Latitude = 39.03540000m, Longitude = -94.57670000m, City = "Kansas City", State = "MO", CreatedAt = zipNow },
                    new() { ZipCode = "64111", Latitude = 39.05970000m, Longitude = -94.59390000m, City = "Kansas City", State = "MO", CreatedAt = zipNow },
                    new() { ZipCode = "90210", Latitude = 34.09010000m, Longitude = -118.40650000m, City = "Beverly Hills", State = "CA", CreatedAt = zipNow },
                    new() { ZipCode = "90001", Latitude = 33.97310000m, Longitude = -118.24790000m, City = "Los Angeles", State = "CA", CreatedAt = zipNow },
                    new() { ZipCode = "94102", Latitude = 37.77930000m, Longitude = -122.41930000m, City = "San Francisco", State = "CA", CreatedAt = zipNow },
                    new() { ZipCode = "92101", Latitude = 32.72130000m, Longitude = -117.16520000m, City = "San Diego", State = "CA", CreatedAt = zipNow },
                    new() { ZipCode = "10001", Latitude = 40.75060000m, Longitude = -73.99720000m, City = "New York", State = "NY", CreatedAt = zipNow },
                    new() { ZipCode = "10002", Latitude = 40.71580000m, Longitude = -73.98700000m, City = "New York", State = "NY", CreatedAt = zipNow },
                    new() { ZipCode = "75201", Latitude = 32.78760000m, Longitude = -96.79940000m, City = "Dallas", State = "TX", CreatedAt = zipNow },
                    new() { ZipCode = "77001", Latitude = 29.83010000m, Longitude = -95.43420000m, City = "Houston", State = "TX", CreatedAt = zipNow },
                    new() { ZipCode = "60601", Latitude = 41.88530000m, Longitude = -87.62290000m, City = "Chicago", State = "IL", CreatedAt = zipNow },
                    new() { ZipCode = "60602", Latitude = 41.88370000m, Longitude = -87.62980000m, City = "Chicago", State = "IL", CreatedAt = zipNow },
                };
                context.ZipCodeLookups.AddRange(zipCodes);
                await context.SaveChangesAsync();
            }
        }
        catch (Exception)
        {
            // ZipCodeLookup table may not exist yet (run migration or delete __EFMigrationsHistory row for AddZipCodeLookup and restart)
            // Continue so app starts; restaurant list will still work
        }

        // Check if restaurant data already exists
        if (context.Restaurants.Any())
        {
            return; // Data already seeded
        }

        var ownerId = Guid.NewGuid(); // Default owner for seed data
        var now = DateTime.UtcNow;

        var restaurants = new List<Restaurant>
        {
            new Restaurant
            {
                RestaurantId = Guid.NewGuid(),
                OwnerId = ownerId,
                Name = "Traditional Kitchen",
                Description = "Authentic traditional cuisine with recipes passed down through generations. Experience the rich flavors of home-cooked meals.",
                CuisineType = "Traditional",
                Address = "123 Main Street, Downtown",
                Latitude = 40.7128,
                Longitude = -74.0060,
                PhoneNumber = "+1-555-0101",
                Email = "info@traditionalkitchen.com",
                IsActive = true,
                Rating = 4.5m,
                ReviewCount = 120,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Restaurant
            {
                RestaurantId = Guid.NewGuid(),
                OwnerId = ownerId,
                Name = "Heritage Bistro",
                Description = "A cozy bistro serving traditional dishes with a modern twist. Family recipes meet contemporary presentation.",
                CuisineType = "Traditional",
                Address = "456 Oak Avenue, Midtown",
                Latitude = 40.7589,
                Longitude = -73.9851,
                PhoneNumber = "+1-555-0102",
                Email = "contact@heritagebistro.com",
                IsActive = true,
                Rating = 4.8m,
                ReviewCount = 89,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Restaurant
            {
                RestaurantId = Guid.NewGuid(),
                OwnerId = ownerId,
                Name = "Mama's Home Cooking",
                Description = "Just like mom used to make! Traditional comfort food that warms your heart and soul.",
                CuisineType = "Traditional",
                Address = "789 Elm Street, Uptown",
                Latitude = 40.7505,
                Longitude = -73.9934,
                PhoneNumber = "+1-555-0103",
                Email = "hello@mamashome.com",
                IsActive = true,
                Rating = 4.7m,
                ReviewCount = 156,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Restaurant
            {
                RestaurantId = Guid.NewGuid(),
                OwnerId = ownerId,
                Name = "Golden Wok",
                Description = "Authentic Asian cuisine with traditional recipes from across the continent. Fresh ingredients, bold flavors.",
                CuisineType = "Asian",
                Address = "321 Pine Street, Chinatown",
                Latitude = 40.7158,
                Longitude = -73.9970,
                PhoneNumber = "+1-555-0104",
                Email = "info@goldenwok.com",
                IsActive = true,
                Rating = 4.6m,
                ReviewCount = 203,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Restaurant
            {
                RestaurantId = Guid.NewGuid(),
                OwnerId = ownerId,
                Name = "Bella Italia",
                Description = "Traditional Italian cuisine with recipes from Nonna's kitchen. Fresh pasta, authentic sauces, and warm hospitality.",
                CuisineType = "Italian",
                Address = "654 Maple Drive, Little Italy",
                Latitude = 40.7181,
                Longitude = -73.9962,
                PhoneNumber = "+1-555-0105",
                Email = "reservations@bellaitalia.com",
                IsActive = true,
                Rating = 4.9m,
                ReviewCount = 312,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Restaurant
            {
                RestaurantId = Guid.NewGuid(),
                OwnerId = ownerId,
                Name = "BBQ Pit Master",
                Description = "Slow-smoked meats and traditional barbecue sides. Our pit masters have been perfecting these recipes for decades.",
                CuisineType = "BBQ",
                Address = "987 Cedar Lane, Riverside",
                Latitude = 40.7614,
                Longitude = -73.9776,
                PhoneNumber = "+1-555-0106",
                Email = "smoke@bbqpitmaster.com",
                IsActive = true,
                Rating = 4.4m,
                ReviewCount = 178,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Restaurant
            {
                RestaurantId = Guid.NewGuid(),
                OwnerId = ownerId,
                Name = "Ocean's Bounty",
                Description = "Fresh seafood prepared with traditional techniques. From the ocean to your table, we bring you the finest catch.",
                CuisineType = "Seafood",
                Address = "147 Harbor View, Waterfront",
                Latitude = 40.7282,
                Longitude = -74.0776,
                PhoneNumber = "+1-555-0107",
                Email = "catch@oceansbounty.com",
                IsActive = true,
                Rating = 4.3m,
                ReviewCount = 95,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Restaurant
            {
                RestaurantId = Guid.NewGuid(),
                OwnerId = ownerId,
                Name = "Green Garden",
                Description = "Plant-based traditional dishes that celebrate vegetables. Healthy, delicious, and environmentally conscious.",
                CuisineType = "Vegetarian",
                Address = "258 Garden Path, Park District",
                Latitude = 40.7829,
                Longitude = -73.9654,
                PhoneNumber = "+1-555-0108",
                Email = "hello@greengarden.com",
                IsActive = true,
                Rating = 4.6m,
                ReviewCount = 67,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Restaurant
            {
                RestaurantId = Guid.NewGuid(),
                OwnerId = ownerId,
                Name = "Sweet Traditions",
                Description = "Traditional desserts and pastries made with love. From grandma's cookie recipes to classic cakes.",
                CuisineType = "Desserts",
                Address = "369 Sugar Street, Sweet District",
                Latitude = 40.7505,
                Longitude = -73.9934,
                PhoneNumber = "+1-555-0109",
                Email = "sweet@sweettraditions.com",
                IsActive = true,
                Rating = 4.8m,
                ReviewCount = 134,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Restaurant
            {
                RestaurantId = Guid.NewGuid(),
                OwnerId = ownerId,
                Name = "Quick Bites",
                Description = "Fast traditional food without compromising on taste. Quick service, authentic flavors.",
                CuisineType = "Fast Food",
                Address = "741 Fast Lane, Business District",
                Latitude = 40.7580,
                Longitude = -73.9855,
                PhoneNumber = "+1-555-0110",
                Email = "quick@quickbites.com",
                IsActive = true,
                Rating = 4.2m,
                ReviewCount = 245,
                CreatedAt = now,
                UpdatedAt = now
            }
        };

        context.Restaurants.AddRange(restaurants);
        await context.SaveChangesAsync();
    }
}
