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
    private readonly ICartSessionService _cartSessionService;
    private const string SESSION_COOKIE_NAME = "cart_session";

    public WebBffController(
        IHttpClientFactory httpClientFactory, 
        ILogger<WebBffController> logger,
        ICartSessionService cartSessionService)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _cartSessionService = cartSessionService;
    }

    private async Task<string> GetOrCreateSessionIdAsync()
    {
        // Try to get session ID from header first (for Blazor WebAssembly)
        string? existingSessionId = null;
        if (Request.Headers.TryGetValue("X-Cart-Session-Id", out var headerSessionId))
        {
            var headerValue = headerSessionId.ToString();
            if (!string.IsNullOrWhiteSpace(headerValue))
            {
                existingSessionId = headerValue;
            }
        }
        
        // Fallback to cookie (for server-side scenarios)
        if (string.IsNullOrEmpty(existingSessionId) && Request.Cookies.TryGetValue(SESSION_COOKIE_NAME, out var cookieSessionId) && !string.IsNullOrEmpty(cookieSessionId))
        {
            existingSessionId = cookieSessionId;
        }

        // Get or create session ID using CartSessionService
        var sessionId = await _cartSessionService.GetOrCreateSessionIdAsync(existingSessionId);

        // If we got a new session ID, set it in the cookie (for server-side scenarios)
        // Note: For Blazor WebAssembly, the client manages the session ID in localStorage
        if (existingSessionId != sessionId && string.IsNullOrEmpty(existingSessionId))
        {
            Response.Cookies.Append(SESSION_COOKIE_NAME, sessionId, new CookieOptions
            {
                HttpOnly = true,
                Secure = false, // Set to true in production with HTTPS
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });
        }

        return sessionId;
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

            // Get guest session ID before login (for cart merge)
            var guestSessionId = await GetOrCreateSessionIdAsync();
            var guestCartId = await _cartSessionService.GetCartIdForSessionAsync(guestSessionId);

            var client = _httpClientFactory.CreateClient("IdentityService");
            var response = await client.PostAsJsonAsync("/api/auth/login", request);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                
                // Parse the login response to get user ID and merge carts
                try
                {
                    var loginResponse = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(content);
                    string? userIdString = null;
                    
                    // Try to get userId from various possible response formats
                    if (loginResponse.TryGetProperty("userId", out var userIdElement))
                    {
                        userIdString = userIdElement.GetString();
                    }
                    else if (loginResponse.TryGetProperty("user", out var userElement) && 
                             userElement.TryGetProperty("id", out var idElement))
                    {
                        userIdString = idElement.GetString();
                    }
                    else if (loginResponse.TryGetProperty("id", out var idElement2))
                    {
                        userIdString = idElement2.GetString();
                    }

                    // If we have a user ID and a guest cart, merge them
                    if (!string.IsNullOrEmpty(userIdString) && Guid.TryParse(userIdString, out var userId) && guestCartId.HasValue)
                    {
                        _logger.LogInformation("Merging guest cart {GuestCartId} into user cart for user {UserId}", 
                            guestCartId.Value, userId);
                        
                        var userCartId = await _cartSessionService.GetCartIdForUserAsync(userId);
                        
                        // Call OrderService to merge carts
                        var orderClient = _httpClientFactory.CreateClient("OrderService");
                        var mergeUrl = userCartId.HasValue
                            ? $"/api/order/cart/merge?guestCartId={guestCartId.Value}&userCartId={userCartId.Value}"
                            : $"/api/order/cart/merge?guestCartId={guestCartId.Value}&userCartId={Guid.Empty}";
                        
                        var mergeResponse = await orderClient.PostAsync(mergeUrl, null);
                        
                        if (mergeResponse.IsSuccessStatusCode)
                        {
                            var mergeContent = await mergeResponse.Content.ReadAsStringAsync();
                            var mergedCart = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(mergeContent);
                            if (mergedCart.TryGetProperty("cartId", out var mergedCartIdElement))
                            {
                                var finalCartId = Guid.Parse(mergedCartIdElement.GetString()!);
                                await _cartSessionService.StoreCartIdForUserAsync(userId, finalCartId);
                                await _cartSessionService.ClearSessionCartAsync(guestSessionId);
                                _logger.LogInformation("Successfully merged carts. Final cart ID: {CartId}", finalCartId);
                            }
                        }
                        else
                        {
                            // If merge fails, just transfer guest cart to user
                            await _cartSessionService.StoreCartIdForUserAsync(userId, guestCartId.Value);
                            await _cartSessionService.ClearSessionCartAsync(guestSessionId);
                            _logger.LogWarning("Cart merge failed, transferred guest cart to user");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse login response or merge carts, continuing with login");
                }
                
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
        try
        {
            // Extract customerId from JWT token if user is authenticated
            Guid? customerId = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
                {
                    customerId = userId;
                }
            }

            var client = _httpClientFactory.CreateClient("OrderService");
            
            // Forward JWT token to OrderService if present
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/order/cart");
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
            }
            
            var requestBody = new CreateCartRequest(request?.RestaurantId);
            httpRequestMessage.Content = System.Net.Http.Json.JsonContent.Create(requestBody);
            
            var response = await client.SendAsync(httpRequestMessage);
            var content = await response.Content.ReadAsStringAsync();
            
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
                            var sessionId = await GetOrCreateSessionIdAsync();
                            await _cartSessionService.StoreCartIdForSessionAsync(sessionId, cartId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse cartId from response");
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
        try
        {
            // For authenticated users, prioritize getting cart by customerId
            if (User.Identity?.IsAuthenticated == true)
            {
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
                    return Content(content, "application/json");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return Ok((object?)null);
                }
                
                return StatusCode((int)response.StatusCode, content);
            }

            // For guest users, try to get cartId from Redis session
            var sessionId = await GetOrCreateSessionIdAsync();
            var cartId = await _cartSessionService.GetCartIdForSessionAsync(sessionId);
            
            if (cartId.HasValue)
            {
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
                    await _cartSessionService.ClearSessionCartAsync(sessionId);
                    return Ok((object?)null);
                }
            }

            // No cart found
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
        try
        {
            var client = _httpClientFactory.CreateClient("OrderService");
            var response = await client.GetAsync($"/api/order/cart/{cartId}");
            var content = await response.Content.ReadAsStringAsync();
            
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
        try
        {
            if (request == null)
            {
                _logger.LogWarning("AddItemToCart: Request body is null");
                return BadRequest(new { error = "Request body is required" });
            }

            _logger.LogInformation("AddItemToCart: CartId={CartId}, MenuItemId={MenuItemId}, Quantity={Quantity}", 
                cartId, request.MenuItemId, request.Quantity);

            var client = _httpClientFactory.CreateClient("OrderService");
            
            // Forward JWT token to OrderService if present
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/order/cart/{cartId}/items");
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
            }
            httpRequestMessage.Content = System.Net.Http.Json.JsonContent.Create(request);
            
            var response = await client.SendAsync(httpRequestMessage);
            var content = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("AddItemToCart: Successfully added item to cart {CartId}", cartId);
                
                // Ensure cartId is stored in Redis session for guest users
                // This handles the case where cart was created but session wasn't set yet
                if (User.Identity?.IsAuthenticated != true)
                {
                    var sessionId = await GetOrCreateSessionIdAsync();
                    var existingCartId = await _cartSessionService.GetCartIdForSessionAsync(sessionId);
                    if (!existingCartId.HasValue || existingCartId.Value != cartId)
                    {
                        await _cartSessionService.StoreCartIdForSessionAsync(sessionId, cartId);
                        _logger.LogInformation("AddItemToCart: Stored cartId {CartId} for session {SessionId}", cartId, sessionId);
                    }
                }
                else
                {
                    // For authenticated users, store in user cart
                    var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
                    {
                        var existingCartId = await _cartSessionService.GetCartIdForUserAsync(userId);
                        if (!existingCartId.HasValue || existingCartId.Value != cartId)
                        {
                            await _cartSessionService.StoreCartIdForUserAsync(userId, cartId);
                            _logger.LogInformation("AddItemToCart: Stored cartId {CartId} for user {UserId}", cartId, userId);
                        }
                    }
                }
                
                // Return success response
                return Ok(new { success = true, cartId = cartId });
            }
            else
            {
                _logger.LogWarning("AddItemToCart: OrderService returned error: {StatusCode}, {Content}", response.StatusCode, content);
                return StatusCode((int)response.StatusCode, content);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AddItemToCart: Error adding item to cart: {Message}", ex.Message);
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
