using Microsoft.AspNetCore.Mvc;

namespace TraditionalEats.Web.Bff.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebBffController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebBffController> _logger;

    public WebBffController(IHttpClientFactory httpClientFactory, ILogger<WebBffController> logger)
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

    [HttpGet("restaurants/{restaurantId}/menu")]
    public async Task<IActionResult> GetRestaurantMenu(Guid restaurantId, [FromQuery] Guid? categoryId = null)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("CatalogService");
            var queryString = categoryId.HasValue ? $"?categoryId={categoryId.Value}" : "";
            var response = await client.GetAsync($"/api/catalog/restaurants/{restaurantId}/menu-items{queryString}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }

            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching restaurant menu");
            return StatusCode(500, new { error = "Failed to fetch restaurant menu" });
        }
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("CatalogService");
            var response = await client.GetAsync("/api/catalog/categories");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }

            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching categories");
            return StatusCode(500, new { error = "Failed to fetch categories" });
        }
    }

    [HttpPost("auth/register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("IdentityService");
            var response = await client.PostAsJsonAsync("/api/auth/register", request);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, errorContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration");
            return StatusCode(500, new { error = "Failed to register" });
        }
    }

    [HttpPost("auth/login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { error = "Email and password are required" });
            }

            var client = _httpClientFactory.CreateClient("IdentityService");
            var response = await client.PostAsJsonAsync("/api/auth/login", request);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, errorContent);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during login. IdentityService may not be running or unreachable.");
            return StatusCode(500, new { error = "Failed to connect to authentication service. Please ensure IdentityService is running." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return StatusCode(500, new { error = "Failed to login" });
        }
    }

    [HttpPost("auth/refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("IdentityService");
            var response = await client.PostAsJsonAsync("/api/auth/refresh", request);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, errorContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token");
            return StatusCode(500, new { error = "Failed to refresh token" });
        }
    }

    [HttpPost("auth/logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("IdentityService");
            var response = await client.PostAsJsonAsync("/api/auth/logout", request);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, errorContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, new { error = "Failed to logout" });
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "WebBff" });
    }
}

public record RegisterRequest(string Email, string? PhoneNumber, string Password, string? Role);
public record LoginRequest(string Email, string Password);
public record RefreshTokenRequest(string RefreshToken);
