using Microsoft.EntityFrameworkCore;
using TraditionEats.CustomerService.Entities;

namespace TraditionEats.CustomerService.Data;

public class CustomerDbContext : DbContext
{
    public CustomerDbContext(DbContextOptions<CustomerDbContext> options) : base(options)
    {
    }

    public DbSet<Customer> Customers { get; set; }
    public DbSet<CustomerPII> CustomerPIIs { get; set; }
    public DbSet<Address> Addresses { get; set; }
    public DbSet<CustomerPreference> Preferences { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.CustomerId);
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.Property(e => e.DisplayName).HasMaxLength(255);
        });

        modelBuilder.Entity<CustomerPII>(entity =>
        {
            entity.HasKey(e => e.CustomerId);
            entity.HasOne(e => e.Customer).WithOne(c => c.PII).HasForeignKey<CustomerPII>(e => e.CustomerId);
            entity.Property(e => e.FirstNameEnc).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.LastNameEnc).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.PhoneEnc).HasMaxLength(1000);
            entity.Property(e => e.EmailEnc).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.PhoneHash).HasMaxLength(500);
            entity.Property(e => e.EmailHash).IsRequired().HasMaxLength(500);
            entity.HasIndex(e => e.EmailHash);
            entity.HasIndex(e => e.PhoneHash);
        });

        modelBuilder.Entity<Address>(entity =>
        {
            entity.HasKey(e => e.AddressId);
            entity.HasIndex(e => e.CustomerId);
            entity.Property(e => e.Line1Enc).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Line2Enc).HasMaxLength(1000);
            entity.Property(e => e.CityEnc).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.ZipEnc).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.GeoHash).HasMaxLength(50);
        });

        modelBuilder.Entity<CustomerPreference>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CustomerId);
        });
    }
}
