using Microsoft.EntityFrameworkCore;
using TraditionalEats.IdentityService.Entities;

namespace TraditionalEats.IdentityService.Data;

public static class SeedData
{
    public static async Task SeedAsync(IdentityDbContext context)
    {
        // Seed roles if they don't exist
        var roles = new[]
        {
            new Role { Id = Guid.NewGuid(), Name = "Customer", Description = "Regular customer who can place orders" },
            new Role { Id = Guid.NewGuid(), Name = "Vendor", Description = "Restaurant owner who can manage their restaurant and menu" },
            new Role { Id = Guid.NewGuid(), Name = "Admin", Description = "Administrator with full system access" },
            new Role { Id = Guid.NewGuid(), Name = "Driver", Description = "Delivery driver" }
        };

        foreach (var role in roles)
        {
            if (!await context.Roles.AnyAsync(r => r.Name == role.Name))
            {
                context.Roles.Add(role);
            }
        }

        await context.SaveChangesAsync();
    }
}
