using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TraditionalEats.DocumentService.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DocumentDbContext>
{
    public DocumentDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DocumentDbContext>();
        optionsBuilder.UseMySql(
            "server=localhost;port=3306;user=root;password=root;database=traditional_eats_document",
            new MySqlServerVersion(new Version(8, 0, 0)));

        return new DocumentDbContext(optionsBuilder.Options);
    }
}
