using Microsoft.EntityFrameworkCore;
using TraditionEats.BuildingBlocks.Encryption;
using TraditionEats.CustomerService.Data;
using TraditionEats.CustomerService.Entities;

namespace TraditionEats.CustomerService.Services;

public interface ICustomerService
{
    Task<Guid> CreateCustomerAsync(Guid userId, string firstName, string lastName, string email, string? phoneNumber);
    Task<Customer?> GetCustomerByUserIdAsync(Guid userId);
    Task<Guid> AddAddressAsync(Guid customerId, AddressDto address);
    Task<List<AddressDto>> GetAddressesAsync(Guid customerId);
}

public class CustomerService : ICustomerService
{
    private readonly CustomerDbContext _context;
    private readonly IPiiEncryptionService _encryption;
    private readonly ILogger<CustomerService> _logger;

    public CustomerService(
        CustomerDbContext context,
        IPiiEncryptionService encryption,
        ILogger<CustomerService> logger)
    {
        _context = context;
        _encryption = encryption;
        _logger = logger;
    }

    public async Task<Guid> CreateCustomerAsync(Guid userId, string firstName, string lastName, string email, string? phoneNumber)
    {
        var customerId = Guid.NewGuid();

        var customer = new Customer
        {
            CustomerId = customerId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var pii = new CustomerPII
        {
            CustomerId = customerId,
            FirstNameEnc = _encryption.Encrypt(firstName),
            LastNameEnc = _encryption.Encrypt(lastName),
            EmailEnc = _encryption.Encrypt(email),
            PhoneEnc = phoneNumber != null ? _encryption.Encrypt(phoneNumber) : null,
            EmailHash = _encryption.HashForSearch(email),
            PhoneHash = phoneNumber != null ? _encryption.HashForSearch(phoneNumber) : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Customers.Add(customer);
        _context.CustomerPIIs.Add(pii);
        await _context.SaveChangesAsync();

        return customerId;
    }

    public async Task<Customer?> GetCustomerByUserIdAsync(Guid userId)
    {
        return await _context.Customers
            .Include(c => c.PII)
            .Include(c => c.Addresses)
            .Include(c => c.Preferences)
            .FirstOrDefaultAsync(c => c.UserId == userId);
    }

    public async Task<Guid> AddAddressAsync(Guid customerId, AddressDto address)
    {
        var addressId = Guid.NewGuid();
        var addressEntity = new Address
        {
            AddressId = addressId,
            CustomerId = customerId,
            Line1Enc = _encryption.Encrypt(address.Line1),
            Line2Enc = address.Line2 != null ? _encryption.Encrypt(address.Line2) : null,
            CityEnc = _encryption.Encrypt(address.City),
            StateEnc = _encryption.Encrypt(address.State),
            ZipEnc = _encryption.Encrypt(address.ZipCode),
            Latitude = address.Latitude,
            Longitude = address.Longitude,
            GeoHash = address.GeoHash,
            IsDefault = address.IsDefault,
            Label = address.Label,
            CreatedAt = DateTime.UtcNow
        };

        if (address.IsDefault)
        {
            // Unset other default addresses
            await _context.Addresses
                .Where(a => a.CustomerId == customerId && a.IsDefault)
                .ExecuteUpdateAsync(a => a.SetProperty(x => x.IsDefault, false));
        }

        _context.Addresses.Add(addressEntity);
        await _context.SaveChangesAsync();

        return addressId;
    }

    public async Task<List<AddressDto>> GetAddressesAsync(Guid customerId)
    {
        var addresses = await _context.Addresses
            .Where(a => a.CustomerId == customerId)
            .ToListAsync();

        return addresses.Select(a => new AddressDto(
            a.AddressId,
            _encryption.Decrypt(a.Line1Enc),
            a.Line2Enc != null ? _encryption.Decrypt(a.Line2Enc) : null,
            _encryption.Decrypt(a.CityEnc),
            _encryption.Decrypt(a.StateEnc),
            _encryption.Decrypt(a.ZipEnc),
            a.Latitude,
            a.Longitude,
            a.GeoHash,
            a.IsDefault,
            a.Label
        )).ToList();
    }
}

public record AddressDto(
    Guid? AddressId,
    string Line1,
    string? Line2,
    string City,
    string State,
    string ZipCode,
    double? Latitude,
    double? Longitude,
    string? GeoHash,
    bool IsDefault,
    string? Label
);
