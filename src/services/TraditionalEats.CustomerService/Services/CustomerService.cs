using Microsoft.EntityFrameworkCore;
using TraditionalEats.BuildingBlocks.Encryption;
using TraditionalEats.CustomerService.Data;
using TraditionalEats.CustomerService.Entities;

namespace TraditionalEats.CustomerService.Services;

public record CustomerInfoDto(Guid CustomerId, Guid UserId, string FirstName, string LastName, string? Email, string? PhoneNumber);

public interface ICustomerService
{
    Task<Guid> CreateCustomerAsync(Guid userId, string firstName, string lastName, string email, string phoneNumber, string? displayName = null);
    Task<Customer?> GetCustomerByUserIdAsync(Guid userId);
    Task<CustomerInfoDto?> GetCustomerInfoByUserIdAsync(Guid userId);
    Task<bool> UpdateCustomerPIIAsync(Guid userId, string firstName, string lastName, string? phoneNumber);
    Task<Guid> AddAddressAsync(Guid customerId, AddressDto address);
    Task<bool> UpdateAddressAsync(Guid userId, Guid addressId, AddressDto address);
    Task<bool> DeleteAddressAsync(Guid userId, Guid addressId);
    Task<List<AddressDto>> GetAddressesAsync(Guid customerId);
    Task<bool> DeleteCustomerByUserIdAsync(Guid userId);
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

    public async Task<Guid> CreateCustomerAsync(Guid userId, string firstName, string lastName, string email, string phoneNumber, string? displayName = null)
    {
        var customerId = Guid.NewGuid();

        var customer = new Customer
        {
            CustomerId = customerId,
            UserId = userId,
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var pii = new CustomerPII
        {
            CustomerId = customerId,
            FirstNameEnc = _encryption.Encrypt(firstName),
            LastNameEnc = _encryption.Encrypt(lastName),
            EmailEnc = _encryption.Encrypt(email),
            PhoneEnc = !string.IsNullOrWhiteSpace(phoneNumber) ? _encryption.Encrypt(phoneNumber) : null,
            EmailHash = _encryption.HashForSearch(email),
            PhoneHash = !string.IsNullOrWhiteSpace(phoneNumber) ? _encryption.HashForSearch(phoneNumber) : null,
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

    public async Task<CustomerInfoDto?> GetCustomerInfoByUserIdAsync(Guid userId)
    {
        var customer = await _context.Customers
            .Include(c => c.PII)
            .FirstOrDefaultAsync(c => c.UserId == userId);
        if (customer?.PII == null)
            return null;
        var pii = customer.PII;
        return new CustomerInfoDto(
            customer.CustomerId,
            customer.UserId,
            _encryption.Decrypt(pii.FirstNameEnc),
            _encryption.Decrypt(pii.LastNameEnc),
            _encryption.Decrypt(pii.EmailEnc),
            pii.PhoneEnc != null ? _encryption.Decrypt(pii.PhoneEnc) : null);
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

    public async Task<bool> UpdateCustomerPIIAsync(Guid userId, string firstName, string lastName, string? phoneNumber)
    {
        var customer = await _context.Customers.Include(c => c.PII).FirstOrDefaultAsync(c => c.UserId == userId);
        if (customer?.PII == null) return false;

        customer.PII.FirstNameEnc = _encryption.Encrypt(firstName);
        customer.PII.LastNameEnc = _encryption.Encrypt(lastName);
        // Email is not updated - it's tied to the Identity account and cannot be changed here
        customer.PII.PhoneEnc = !string.IsNullOrWhiteSpace(phoneNumber) ? _encryption.Encrypt(phoneNumber) : null;
        customer.PII.PhoneHash = !string.IsNullOrWhiteSpace(phoneNumber) ? _encryption.HashForSearch(phoneNumber) : null;
        customer.PII.UpdatedAt = DateTime.UtcNow;
        customer.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateAddressAsync(Guid userId, Guid addressId, AddressDto address)
    {
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
        if (customer == null) return false;

        var addr = await _context.Addresses.FirstOrDefaultAsync(a => a.AddressId == addressId && a.CustomerId == customer.CustomerId);
        if (addr == null) return false;

        addr.Line1Enc = _encryption.Encrypt(address.Line1);
        addr.Line2Enc = address.Line2 != null ? _encryption.Encrypt(address.Line2) : null;
        addr.CityEnc = _encryption.Encrypt(address.City);
        addr.StateEnc = _encryption.Encrypt(address.State);
        addr.ZipEnc = _encryption.Encrypt(address.ZipCode);
        addr.Latitude = address.Latitude;
        addr.Longitude = address.Longitude;
        addr.GeoHash = address.GeoHash;
        addr.IsDefault = address.IsDefault;
        addr.Label = address.Label;

        if (address.IsDefault)
        {
            await _context.Addresses
                .Where(a => a.CustomerId == customer.CustomerId && a.AddressId != addressId && a.IsDefault)
                .ExecuteUpdateAsync(a => a.SetProperty(x => x.IsDefault, false));
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAddressAsync(Guid userId, Guid addressId)
    {
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
        if (customer == null) return false;

        var addr = await _context.Addresses.FirstOrDefaultAsync(a => a.AddressId == addressId && a.CustomerId == customer.CustomerId);
        if (addr == null) return false;

        _context.Addresses.Remove(addr);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteCustomerByUserIdAsync(Guid userId)
    {
        var customer = await _context.Customers
            .Include(c => c.PII)
            .Include(c => c.Addresses)
            .Include(c => c.Preferences)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (customer == null)
            return false;

        if (customer.Addresses?.Any() == true)
            _context.Addresses.RemoveRange(customer.Addresses);
        if (customer.Preferences?.Any() == true)
            _context.Preferences.RemoveRange(customer.Preferences);
        if (customer.PII != null)
            _context.CustomerPIIs.Remove(customer.PII);

        _context.Customers.Remove(customer);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Customer deleted for userId {UserId}", userId);
        return true;
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
