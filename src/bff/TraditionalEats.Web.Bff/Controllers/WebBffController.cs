using Microsoft.AspNetCore.Mvc;
using TraditionalEats.BuildingBlocks.Redis;
using System.Security.Claims;

namespace TraditionalEats.Web.Bff.Controllers;

[ApiController]
[Route("api/WebBff")]
public class WebBffController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebBffController> _logger;
    private readonly IRedisService _redis;
    private const string SESSION_COOKIE_NAME = "cart_session";
    private const string CART_SESSION_PREFIX = "cart_session:";

    public WebBffController(
        IHttpClientFactory httpClientFactory, 
        ILogger<WebBffController> logger,
        IRedisService redis)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _redis = redis;
    }

    private string GetOrCreateSessionId()
    {
        // Try to get session ID from cookie
        if (Request.Cookies.TryGetValue(SESSION_COOKIE_NAME, out var sessionId) && !string.IsNullOrEmpty(sessionId))
        {
            return sessionId;
        }

        // Create new session ID
        sessionId = Guid.NewGuid().ToString();
        Response.Cookies.Append(SESSION_COOKIE_NAME, sessionId, new CookieOptions
        {
            HttpOnly = true,
            Secure = false, // Set to true in production with HTTPS
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        });

        return sessionId;
    }

    private async Task<Guid?> GetCartIdFromSessionAsync()
    {
        var sessionId = GetOrCreateSessionId();
        var cartIdString = await _redis.GetAsync<string>($"{CART_SESSION_PREFIX}{sessionId}");
        if (Guid.TryParse(cartIdString, out var cartId))
        {
            return cartId;
        }
        return null;
    }

    private async Task StoreCartIdInSessionAsync(Guid cartId)
    {
        var sessionId = GetOrCreateSessionId();
        await _redis.SetAsync($"{CART_SESSION_PREFIX}{sessionId}", cartId.ToString(), TimeSpan.FromDays(30));
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
            
            // Forward JWT token to OrderService
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/order");
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
            }
            
            var response = await client.SendAsync(httpRequestMessage);
            var content = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                return Ok(content);
            }
            
            return StatusCode((int)response.StatusCode, content);
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

    [HttpPost("cart")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> CreateCart([FromBody] CreateCartRequest? request = null)
    {
        _logger.LogInformation("CreateCart called with request: {@Request}", request);
        try
        {
            // Log authentication state
            _logger.LogInformation("User.Identity.IsAuthenticated: {IsAuthenticated}", User.Identity?.IsAuthenticated ?? false);
            
            // Extract customerId from JWT token if user is authenticated
            Guid? customerId = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                _logger.LogInformation("Found userIdClaim: {UserIdClaim}", userIdClaim ?? "null");
                if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
                {
                    customerId = userId;
                    _logger.LogInformation("Extracted customerId {CustomerId} from JWT token", customerId);
                }
                else
                {
                    _logger.LogWarning("Failed to parse userIdClaim as Guid: {UserIdClaim}", userIdClaim);
                }
            }
            else
            {
                _logger.LogInformation("User is not authenticated");
            }

            var client = _httpClientFactory.CreateClient("OrderService");
            
            // Forward JWT token to OrderService if present
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/order/cart");
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                var authHeaderValue = authHeader.ToString();
                _logger.LogInformation("Forwarding Authorization header to OrderService: {AuthHeader}", 
                    authHeaderValue.Substring(0, Math.Min(20, authHeaderValue.Length)) + "...");
                httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authHeaderValue);
            }
            else
            {
                _logger.LogInformation("No Authorization header found in request");
            }
            
            var requestBody = new CreateCartRequest(request?.RestaurantId);
            httpRequestMessage.Content = System.Net.Http.Json.JsonContent.Create(requestBody);
            
            var response = await client.SendAsync(httpRequestMessage);
            var content = await response.Content.ReadAsStringAsync();
            
            _logger.LogInformation("OrderService response for CreateCart: StatusCode={StatusCode}, Content={Content}", 
                response.StatusCode, content);
            
            if (response.IsSuccessStatusCode)
            {
                // Parse cartId from response and store in Redis session
                try
                {
                    var result = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(content);
                    if (result.TryGetProperty("cartId", out var cartIdElement))
                    {
                        if (Guid.TryParse(cartIdElement.GetString(), out var cartId))
                        {
                            await StoreCartIdInSessionAsync(cartId);
                            _logger.LogInformation("Stored cartId {CartId} in Redis session", cartId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse cartId from response");
                }
            }
            else
            {
                _logger.LogWarning("OrderService returned error: {StatusCode}, {Content}", response.StatusCode, content);
            }
            
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating cart: {Message}", ex.Message);
            return StatusCode(500, new { error = $"Failed to create cart: {ex.Message}" });
        }
    }

    [HttpGet("cart")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> GetCart()
    {
        _logger.LogInformation("GetCart endpoint called");
        try
        {
            // For authenticated users, prioritize getting cart by customerId
            if (User.Identity?.IsAuthenticated == true)
            {
                _logger.LogInformation("User is authenticated, trying to get cart by customer");
                var client = _httpClientFactory.CreateClient("OrderService");
                
                // Forward JWT token to OrderService
                var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/order/cart");
                if (Request.Headers.TryGetValue("Authorization", out var authHeader))
                {
                    httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
                }
                
                var response = await client.SendAsync(httpRequestMessage);
                var content = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Found cart by customerId. Response length: {Length} bytes", content.Length);
                    _logger.LogInformation("Cart response content: {Content}", content);
                    
                    // Try to parse and log cart structure
                    try
                    {
                        var cartJson = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(content);
                        if (cartJson.TryGetProperty("cartId", out var cartIdElement))
                        {
                            _logger.LogInformation("Cart ID: {CartId}", cartIdElement.GetString());
                        }
                        if (cartJson.TryGetProperty("items", out var items))
                        {
                            _logger.LogInformation("Cart has {Count} items", items.GetArrayLength());
                            foreach (var item in items.EnumerateArray())
                            {
                                if (item.TryGetProperty("name", out var name))
                                {
                                    _logger.LogInformation("  Item: {Name}, Quantity: {Quantity}", 
                                        name.GetString(),
                                        item.TryGetProperty("quantity", out var qty) ? qty.GetInt32() : 0);
                                }
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Cart response does not have 'items' property");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse cart JSON");
                    }
                    
                    return Content(content, "application/json");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("No cart found for authenticated user");
                    return Ok((object?)null);
                }
                
                return StatusCode((int)response.StatusCode, content);
            }

            // For guest users, try to get cartId from Redis session
            var cartId = await GetCartIdFromSessionAsync();
            if (cartId.HasValue)
            {
                _logger.LogInformation("Found cartId {CartId} in Redis session for guest user", cartId.Value);
                var client = _httpClientFactory.CreateClient("OrderService");
                var response = await client.GetAsync($"/api/order/cart/{cartId.Value}");
                var content = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    return Content(content, "application/json");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Cart doesn't exist anymore, clear session
                    var sessionId = GetOrCreateSessionId();
                    await _redis.DeleteAsync($"{CART_SESSION_PREFIX}{sessionId}");
                    _logger.LogInformation("Cart not found, cleared session");
                    return Ok((object?)null);
                }
            }

            // No cart found
            _logger.LogInformation("No cart found for guest user");
            return Ok((object?)null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cart: {Message}", ex.Message);
            return StatusCode(500, new { error = $"Failed to get cart: {ex.Message}" });
        }
    }

    [HttpGet("cart/{cartId}")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> GetCartById(Guid cartId)
    {
        _logger.LogInformation("GetCartById called with cartId: {CartId}", cartId);
        try
        {
            var client = _httpClientFactory.CreateClient("OrderService");
            var response = await client.GetAsync($"/api/order/cart/{cartId}");
            var content = await response.Content.ReadAsStringAsync();
            
            _logger.LogInformation("OrderService response for GetCartById: StatusCode={StatusCode}", response.StatusCode);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return Ok((object?)null);
            }
            
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cart by ID: {Message}", ex.Message);
            return StatusCode(500, new { error = $"Failed to get cart: {ex.Message}" });
        }
    }

    [HttpPost("cart/{cartId}/items")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> AddItemToCart(Guid cartId, [FromBody] AddCartItemRequest request)
    {
        _logger.LogInformation("AddItemToCart called with cartId: {CartId}, request: {@Request}", cartId, request);
        try
        {
            if (request == null)
            {
                _logger.LogWarning("AddItemToCart: Request body is null");
                return BadRequest(new { error = "Request body is required" });
            }

            var client = _httpClientFactory.CreateClient("OrderService");
            var response = await client.PostAsJsonAsync($"/api/order/cart/{cartId}/items", request);
            var content = await response.Content.ReadAsStringAsync();
            
            _logger.LogInformation("OrderService response for AddItemToCart: StatusCode={StatusCode}, Content={Content}", 
                response.StatusCode, content);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OrderService returned error: {StatusCode}, {Content}", response.StatusCode, content);
            }
            
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding item to cart: {Message}", ex.Message);
            return StatusCode(500, new { error = $"Failed to add item to cart: {ex.Message}" });
        }
    }

    [HttpPut("cart/{cartId}/items/{cartItemId}")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> UpdateCartItem(Guid cartId, Guid cartItemId, [FromBody] UpdateCartItemRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("OrderService");
            var response = await client.PutAsJsonAsync($"/api/order/cart/{cartId}/items/{cartItemId}", request);
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
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> RemoveCartItem(Guid cartId, Guid cartItemId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("OrderService");
            var response = await client.DeleteAsync($"/api/order/cart/{cartId}/items/{cartItemId}");
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
            
            // Forward JWT token to OrderService
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/order/place");
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
            }
            httpRequestMessage.Content = System.Net.Http.Json.JsonContent.Create(request);
            
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
            
            // Forward JWT token to OrderService
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/order/{orderId}");
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
        return Ok(new { status = "healthy", service = "WebBff" });
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
