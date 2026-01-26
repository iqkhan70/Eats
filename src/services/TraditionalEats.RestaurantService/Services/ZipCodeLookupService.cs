using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TraditionalEats.BuildingBlocks.Geocoding;
using TraditionalEats.RestaurantService.Data;

namespace TraditionalEats.RestaurantService.Services;

/// <summary>
/// ZIP code lookup service implementation for RestaurantService
/// This can be extended to use a database table (like Mental Health app) or an external API
/// </summary>
public class ZipCodeLookupService : IZipCodeLookupService
{
    private readonly ILogger<ZipCodeLookupService> _logger;

    public ZipCodeLookupService(ILogger<ZipCodeLookupService> logger)
    {
        _logger = logger;
    }

    public Task<(decimal Latitude, decimal Longitude)?> GetLatLonFromZipCodeAsync(string zipCode)
    {
        if (string.IsNullOrWhiteSpace(zipCode))
            return Task.FromResult<(decimal Latitude, decimal Longitude)?>(null);

        // TODO: Implement database lookup using ZipCodeLookup table
        // For now, return null - geocoding will fall back to manual entry
        // This can be implemented later when we add the ZipCodeLookup table to RestaurantService
        
        _logger.LogWarning("ZIP code lookup not yet implemented. ZIP code: {ZipCode}", zipCode);
        return Task.FromResult<(decimal Latitude, decimal Longitude)?>(null);
    }
}
