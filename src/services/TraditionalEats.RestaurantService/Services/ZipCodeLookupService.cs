using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TraditionalEats.BuildingBlocks.Geocoding;
using TraditionalEats.RestaurantService.Data;

namespace TraditionalEats.RestaurantService.Services;

/// <summary>
/// ZIP code lookup from database (same approach as mental health app).
/// No external API cost; data seeded from same source as mental health.
/// </summary>
public class ZipCodeLookupService : IZipCodeLookupService
{
    private readonly RestaurantDbContext _context;
    private readonly ILogger<ZipCodeLookupService> _logger;

    public ZipCodeLookupService(RestaurantDbContext context, ILogger<ZipCodeLookupService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<(decimal Latitude, decimal Longitude)?> GetLatLonFromZipCodeAsync(string zipCode)
    {
        if (string.IsNullOrWhiteSpace(zipCode))
            return null;

        var zip5 = zipCode.Trim().Length >= 5 ? zipCode.Trim()[..5] : zipCode.Trim();
        try
        {
            var lookup = await _context.ZipCodeLookups
                .AsNoTracking()
                .FirstOrDefaultAsync(z => z.ZipCode == zip5);

            if (lookup == null)
            {
                _logger.LogDebug("ZIP code {Zip} not found in lookup table", zip5);
                return null;
            }

            return (lookup.Latitude, lookup.Longitude);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up ZIP code {Zip}", zip5);
            return null;
        }
    }
}
