using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace TraditionalEats.PromotionService.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PromotionDbContext>
{
    public PromotionDbContext CreateDbContext(string[] args)
    {
        // Build configuration for design-time
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("PromotionDb")
            ?? "server=localhost;port=3306;database=traditional_eats_promotion;user=root;password=UthmanBasima70";

        var optionsBuilder = new DbContextOptionsBuilder<PromotionDbContext>();
        optionsBuilder.UseMySql(
            connectionString,
            new MySqlServerVersion(new Version(8, 0, 0)));

        return new PromotionDbContext(optionsBuilder.Options);
    }
}
