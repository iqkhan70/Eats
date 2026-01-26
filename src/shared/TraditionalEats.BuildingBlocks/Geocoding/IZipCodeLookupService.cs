namespace TraditionalEats.BuildingBlocks.Geocoding;

/// <summary>
/// Service for looking up latitude/longitude from ZIP codes
/// Can be implemented using a database table (like Mental Health app) or an external API
/// </summary>
public interface IZipCodeLookupService
{
    Task<(decimal Latitude, decimal Longitude)?> GetLatLonFromZipCodeAsync(string zipCode);
}
