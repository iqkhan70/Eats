using Microsoft.Extensions.Logging;

namespace TraditionalEats.BuildingBlocks.Geocoding;

public interface IGeocodingService
{
    /// <summary>
    /// Geocode an address to get latitude and longitude coordinates
    /// </summary>
    /// <param name="address">Full address string (e.g., "123 Main St, New York, NY 10001")</param>
    /// <returns>Tuple of (Latitude, Longitude) or null if geocoding fails</returns>
    Task<(double Latitude, double Longitude)?> GeocodeAddressAsync(string address);

    /// <summary>
    /// Geocode using ZIP code (fallback method)
    /// </summary>
    Task<(double Latitude, double Longitude)?> GeocodeZipCodeAsync(string zipCode);
}

public class GeocodingService : IGeocodingService
{
    private readonly ILogger<GeocodingService> _logger;
    private readonly IZipCodeLookupService? _zipCodeLookup;

    public GeocodingService(ILogger<GeocodingService> logger, IZipCodeLookupService? zipCodeLookup = null)
    {
        _logger = logger;
        _zipCodeLookup = zipCodeLookup;
    }

    public async Task<(double Latitude, double Longitude)?> GeocodeAddressAsync(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            _logger.LogWarning("GeocodeAddressAsync: Address is null or empty");
            return null;
        }

        try
        {
            // Try to extract ZIP code from address and use ZIP lookup as primary method
            var zipCode = ExtractZipCode(address);
            if (!string.IsNullOrEmpty(zipCode) && _zipCodeLookup != null)
            {
                _logger.LogInformation("GeocodeAddressAsync: Extracted ZIP code {ZipCode} from address, using ZIP lookup", zipCode);
                var result = await _zipCodeLookup.GetLatLonFromZipCodeAsync(zipCode);
                if (result.HasValue)
                {
                    return ((double)result.Value.Latitude, (double)result.Value.Longitude);
                }
            }

            // TODO: Integrate with a geocoding API (Google Maps, Mapbox, etc.) for full address geocoding
            // For now, return null if ZIP lookup fails
            _logger.LogWarning("GeocodeAddressAsync: Unable to geocode address '{Address}'. ZIP lookup failed and no geocoding API configured.", address);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GeocodeAddressAsync: Error geocoding address '{Address}'", address);
            return null;
        }
    }

    public async Task<(double Latitude, double Longitude)?> GeocodeZipCodeAsync(string zipCode)
    {
        if (string.IsNullOrWhiteSpace(zipCode))
        {
            return null;
        }

        if (_zipCodeLookup == null)
        {
            _logger.LogWarning("GeocodeZipCodeAsync: ZIP code lookup service not configured");
            return null;
        }

        try
        {
            var result = await _zipCodeLookup.GetLatLonFromZipCodeAsync(zipCode);
            if (result.HasValue)
            {
                return ((double)result.Value.Latitude, (double)result.Value.Longitude);
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GeocodeZipCodeAsync: Error looking up ZIP code {ZipCode}", zipCode);
            return null;
        }
    }

    private string? ExtractZipCode(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return null;

        // US ZIP is almost always at the END of the address (e.g. "...Overland Park KS, 66221").
        // Avoid taking a street number (e.g. "15120 Perry Street...") by using the LAST match.
        // 1) Standard US format: word boundary, 5 digits, optional -4 digits; take last match
        var zipPattern = @"\b\d{5}(?:-\d{4})?\b";
        var matches = System.Text.RegularExpressions.Regex.Matches(address, zipPattern);
        if (matches.Count > 0)
            return matches[matches.Count - 1].Value.Substring(0, 5);

        // 2) Fallback: any standalone 5-digit number (not part of 6+ digits); take last occurrence
        var standaloneFive = System.Text.RegularExpressions.Regex.Matches(address, @"\d{5}(?!\d)");
        if (standaloneFive.Count > 0)
            return standaloneFive[standaloneFive.Count - 1].Value;

        return null;
    }
}
