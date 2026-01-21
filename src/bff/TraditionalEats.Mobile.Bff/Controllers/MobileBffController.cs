using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/order");
            
            // Forward JWT token to OrderService if present
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
            }
            
            var response = await client.SendAsync(httpRequestMessage);
            
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

    [HttpPost("cart")]
    public async Task<IActionResult> CreateCart([FromBody] CreateCartRequest? request = null)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("OrderService");
            var requestBody = request ?? new CreateCartRequest(null);
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/order/cart")
            {
                Content = System.Net.Http.Json.JsonContent.Create(requestBody)
            };
            
            // Forward JWT token to OrderService if present
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
            }
            
            var response = await client.SendAsync(httpRequestMessage);
            var content = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating cart");
            return StatusCode(500, new { error = "Failed to create cart" });
        }
    }

    [HttpGet("cart")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> GetCart()
    {
        _logger.LogInformation("GetCart endpoint called");
        try
        {
            // Check if Authorization header is present
            var hasAuthHeader = Request.Headers.TryGetValue("Authorization", out var authHeader);
            _logger.LogInformation("Authorization header present: {HasAuthHeader}, User authenticated: {IsAuthenticated}", 
                hasAuthHeader, User.Identity?.IsAuthenticated ?? false);
            
            if (hasAuthHeader)
            {
                var authHeaderValue = authHeader.ToString();
                _logger.LogInformation("Authorization header value: {AuthHeader}", 
                    authHeaderValue.Length > 20 ? authHeaderValue.Substring(0, 20) + "..." : authHeaderValue);
            }
            
            // For authenticated users, prioritize getting cart by customerId
            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                _logger.LogInformation("User is authenticated, userId: {UserId}, trying to get cart by customer", userIdClaim);
                var client = _httpClientFactory.CreateClient("OrderService");
                
                // Forward JWT token to OrderService
                var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/order/cart");
                if (hasAuthHeader)
                {
                    httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
                    _logger.LogInformation("Forwarded Authorization header to OrderService");
                }
                else
                {
                    _logger.LogWarning("User is authenticated but no Authorization header found!");
                }
                
                var response = await client.SendAsync(httpRequestMessage);
                var content = await response.Content.ReadAsStringAsync();
                
                _logger.LogInformation("OrderService response: StatusCode={StatusCode}, ContentLength={Length}", 
                    response.StatusCode, content.Length);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Found cart by customerId. Response length: {Length} bytes", content.Length);
                    return Content(content, "application/json");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("No cart found for authenticated user (customerId: {CustomerId})", userIdClaim);
                    return Ok((object?)null);
                }
                
                return StatusCode((int)response.StatusCode, content);
            }

            // For guest users, return null (no session management for mobile - use cartId directly)
            _logger.LogInformation("User is not authenticated (guest) - returning null");
            return Ok((object?)null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cart: {Message}", ex.Message);
            return StatusCode(500, new { error = $"Failed to get cart: {ex.Message}" });
        }
    }

    [HttpGet("cart/{cartId}")]
    public async Task<IActionResult> GetCartById(Guid cartId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("OrderService");
            var response = await client.GetAsync($"/api/order/cart/{cartId}");
            var content = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cart");
            return StatusCode(500, new { error = "Failed to get cart" });
        }
    }

    [HttpPost("cart/{cartId}/items")]
    public async Task<IActionResult> AddItemToCart(Guid cartId, [FromBody] AddCartItemRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("OrderService");
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/order/cart/{cartId}/items")
            {
                Content = System.Net.Http.Json.JsonContent.Create(request)
            };
            
            // Forward JWT token to OrderService if present
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
            }
            
            var response = await client.SendAsync(httpRequestMessage);
            var content = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding item to cart");
            return StatusCode(500, new { error = "Failed to add item to cart" });
        }
    }

    [HttpPut("cart/{cartId}/items/{cartItemId}")]
    public async Task<IActionResult> UpdateCartItem(Guid cartId, Guid cartItemId, [FromBody] UpdateCartItemRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("OrderService");
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Put, $"/api/order/cart/{cartId}/items/{cartItemId}")
            {
                Content = System.Net.Http.Json.JsonContent.Create(request)
            };
            
            // Forward JWT token to OrderService if present
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
            }
            
            var response = await client.SendAsync(httpRequestMessage);
            var content = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating cart item");
            return StatusCode(500, new { error = "Failed to update cart item" });
        }
    }

    [HttpDelete("cart/{cartId}/items/{cartItemId}")]
    public async Task<IActionResult> RemoveCartItem(Guid cartId, Guid cartItemId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("OrderService");
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Delete, $"/api/order/cart/{cartId}/items/{cartItemId}");
            
            // Forward JWT token to OrderService if present
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
            }
            
            var response = await client.SendAsync(httpRequestMessage);
            var content = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cart item");
            return StatusCode(500, new { error = "Failed to remove cart item" });
        }
    }

    [HttpPost("orders/place")]
    public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("OrderService");
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/order/place")
            {
                Content = System.Net.Http.Json.JsonContent.Create(request)
            };
            
            // Forward JWT token to OrderService if present
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
            }
            
            var response = await client.SendAsync(httpRequestMessage);
            var content = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error placing order");
            return StatusCode(500, new { error = "Failed to place order" });
        }
    }

    [HttpGet("orders/{orderId}")]
    public async Task<IActionResult> GetOrder(Guid orderId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("OrderService");
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/order/{orderId}");
            
            // Forward JWT token to OrderService if present
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
            }
            
            var response = await client.SendAsync(httpRequestMessage);
            var content = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting order");
            return StatusCode(500, new { error = "Failed to get order" });
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "MobileBff" });
    }
}

public record CreateCartRequest(Guid? RestaurantId);
public record AddCartItemRequest(
    Guid MenuItemId,
    string Name,
    decimal Price,
    int Quantity,
    Dictionary<string, string>? Options
);
public record UpdateCartItemRequest(int Quantity);
public record PlaceOrderRequest(
    Guid CartId,
    string DeliveryAddress,
    string? IdempotencyKey
);

public record RegisterRequest(string Email, string? PhoneNumber, string Password, string? Role);
public record LoginRequest(string Email, string Password);
public record RefreshTokenRequest(string RefreshToken);
