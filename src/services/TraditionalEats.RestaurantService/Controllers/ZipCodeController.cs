using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TraditionalEats.BuildingBlocks.Geocoding;

namespace TraditionalEats.RestaurantService.Controllers;

/// <summary>
/// ZIP code lookup (lat/lon from DB only). Used by BFF geocode-zip; no external geocoding (Nominatim).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class ZipCodeController : ControllerBase
    private readonly IZipCodeLookupService _zipLookup;
private readonly ILogger<ZipCodeController> _logger;

public ZipCodeController(IZipCodeLookupService zipLookup, ILogger<ZipCodeController> logger)
{
    _zipLookup = zipLookup;
    _logger = logger;
}

[HttpGet("{zip}")]
public async Task<IActionResult> GetLatLon(string zip)
{
    if (string.IsNullOrWhiteSpace(zip))
        return BadRequest(new { message = "zip is required" });
    var zip5 = zip.Trim().Length >= 5 ? zip.Trim()[..5] : zip.Trim();
    try
    {
        var result = await _zipLookup.GetLatLonFromZipCodeAsync(zip5);
        if (!result.HasValue)
            return NotFound(new { message = "ZIP code not found" });
        return Ok(new { latitude = (double)result.Value.Latitude, longitude = (double)result.Value.Longitude });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "ZIP lookup failed for {Zip}", zip5);
        return StatusCode(500, new { message = "ZIP lookup failed" });
    }
}
}
