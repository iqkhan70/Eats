using Microsoft.AspNetCore.Mvc;

namespace TraditionalEats.Mobile.Bff.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MobileBffController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MobileBffController> _logger;

    public MobileBffController(IHttpClientFactory httpClientFactory, ILogger<MobileBffController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

        [HttpGet("restaurants")]
        public async Task<IActionResult> GetRestaurants(
            [FromQuery] string? location,
            [FromQuery] string? cuisineType,
            [FromQuery] double? latitude,
            [FromQuery] double? longitude,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 20)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("RestaurantService");
                
                // Build query string
                var queryParams = new List<string>();
                if (!string.IsNullOrEmpty(location)) queryParams.Add($"location={Uri.EscapeDataString(location)}");
                if (!string.IsNullOrEmpty(cuisineType)) queryParams.Add($"cuisineType={Uri.EscapeDataString(cuisineType)}");
                if (latitude.HasValue) queryParams.Add($"latitude={latitude.Value}");
                if (longitude.HasValue) queryParams.Add($"longitude={longitude.Value}");
                queryParams.Add($"skip={skip}");
                queryParams.Add($"take={take}");
                
                var queryString = queryParams.Any() ? "?" + string.Join("&", queryParams) : "";
                var response = await client.GetAsync($"/api/restaurant{queryString}");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return Content(content, "application/json");
                }
                
                return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching restaurants");
                return StatusCode(500, new { error = "Failed to fetch restaurants" });
            }
        }

    [HttpGet("restaurants/{id}")]
    public async Task<IActionResult> GetRestaurant(Guid id)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("RestaurantService");
            var response = await client.GetAsync($"/api/restaurant/{id}");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return Ok(content);
            }
            
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching restaurant {RestaurantId}", id);
            return StatusCode(500, new { error = "Failed to fetch restaurant" });
        }
    }

    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("OrderService");
            var response = await client.GetAsync("/api/order");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return Ok(content);
            }
            
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching orders");
            return StatusCode(500, new { error = "Failed to fetch orders" });
        }
    }

    [HttpGet("search-suggestions")]
    public async Task<IActionResult> GetSearchSuggestions([FromQuery] string query, [FromQuery] int maxResults = 10)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("RestaurantService");
            var response = await client.GetAsync($"/api/restaurant/search-suggestions?query={Uri.EscapeDataString(query)}&maxResults={maxResults}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }

            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching search suggestions");
            return StatusCode(500, new { error = "Failed to fetch search suggestions" });
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "MobileBff" });
    }
}
