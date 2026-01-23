using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TraditionalEats.BuildingBlocks.Redis;

namespace TraditionalEats.Mobile.Bff.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MobileBffController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MobileBffController> _logger;
    private readonly ICartSessionService _cartSessionService;
    private const string SESSION_HEADER_NAME = "X-Cart-Session-Id";

    public MobileBffController(
        IHttpClientFactory httpClientFactory, 
        ILogger<MobileBffController> logger,
        ICartSessionService cartSessionService)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _cartSessionService = cartSessionService;
    }

    private async Task<string> GetOrCreateSessionIdAsync()
    {
        // Try to get session ID from header (mobile app sends it)
        string? existingSessionId = null;
        if (Request.Headers.TryGetValue(SESSION_HEADER_NAME, out var headerSessionId))
        {
            var headerValue = headerSessionId.ToString();
            if (!string.IsNullOrWhiteSpace(headerValue))
            {
                existingSessionId = headerValue;
            }
        }

        // Get or create session ID using CartSessionService
        var sessionId = await _cartSessionService.GetOrCreateSessionIdAsync(existingSessionId);
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
                            // For authenticated users, store in user cart
                            if (User.Identity?.IsAuthenticated == true)
                            {
                                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                                if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
                                {
                                    await _cartSessionService.StoreCartIdForUserAsync(userId, cartId);
                                }
                            }
                            else
                            {
                                // For guest users, store in session cart
                                var sessionId = await GetOrCreateSessionIdAsync();
                                await _cartSessionService.StoreCartIdForSessionAsync(sessionId, cartId);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse cartId from response");
                }
            }
            
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
        try
        {
            // For authenticated users, prioritize getting cart by customerId
            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                
                if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
                {
                    // Try to get cart ID from Redis first (faster)
                    var userCartId = await _cartSessionService.GetCartIdForUserAsync(userId);
                    if (userCartId.HasValue)
                    {
                        var client = _httpClientFactory.CreateClient("OrderService");
                        var response = await client.GetAsync($"/api/order/cart/{userCartId.Value}");
                        var content = await response.Content.ReadAsStringAsync();
                        
                        if (response.IsSuccessStatusCode)
                        {
                            return Content(content, "application/json");
                        }
                    }
                }
                
                // Fallback to OrderService query by customerId
                var client2 = _httpClientFactory.CreateClient("OrderService");
                var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/order/cart");
                if (Request.Headers.TryGetValue("Authorization", out var authHeader))
                {
                    httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
                }
                
                var response2 = await client2.SendAsync(httpRequestMessage);
                var content2 = await response2.Content.ReadAsStringAsync();
                
                if (response2.IsSuccessStatusCode)
                {
                    // Store cart ID in Redis for future lookups
                    try
                    {
                        var cartJson = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(content2);
                        if (cartJson.TryGetProperty("cartId", out var cartIdElement) && 
                            Guid.TryParse(cartIdElement.GetString(), out var cartId) &&
                            !string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userId2))
                        {
                            await _cartSessionService.StoreCartIdForUserAsync(userId2, cartId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to store cart ID in Redis");
                    }
                    
                    return Content(content2, "application/json");
                }
                else if (response2.StatusCode == System.Net.HttpStatusCode.NotFound || response2.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    return Ok((object?)null);
                }
                
                return StatusCode((int)response2.StatusCode, content2);
            }

            // For guest users, try to get cartId from session
            var sessionId = await GetOrCreateSessionIdAsync();
            var guestCartId = await _cartSessionService.GetCartIdForSessionAsync(sessionId);
            
            if (guestCartId.HasValue)
            {
                var client = _httpClientFactory.CreateClient("OrderService");
                var response = await client.GetAsync($"/api/order/cart/{guestCartId.Value}");
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
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> AddItemToCart(Guid cartId, [FromBody] AddCartItemRequest? request)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(new { error = "Request body is required and must be valid JSON" });
            }
            
            // Validate required fields
            if (request.MenuItemId == Guid.Empty)
            {
                return BadRequest(new { error = "MenuItemId is required and must be a valid GUID" });
            }
            
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { error = "Name is required" });
            }
            
            if (request.Price < 0)
            {
                return BadRequest(new { error = "Price must be non-negative" });
            }
            
            if (request.Quantity <= 0)
            {
                return BadRequest(new { error = "Quantity must be positive" });
            }
            
            _logger.LogInformation("AddItemToCart: CartId={CartId}, MenuItemId={MenuItemId}, Name={Name}, Price={Price}, Quantity={Quantity}", 
                cartId, request.MenuItemId, request.Name, request.Price, request.Quantity);
            
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
            
            _logger.LogInformation("AddItemToCart: OrderService response - StatusCode={StatusCode}, Content={Content}", 
                response.StatusCode, content);
            
            if (response.IsSuccessStatusCode)
            {
                // Ensure cartId is stored in Redis session for guest users
                // This handles the case where cart was created but session wasn't set yet
                if (User.Identity?.IsAuthenticated != true)
                {
                    var sessionId = await GetOrCreateSessionIdAsync();
                    var existingCartId = await _cartSessionService.GetCartIdForSessionAsync(sessionId);
                    if (!existingCartId.HasValue || existingCartId.Value != cartId)
                    {
                        await _cartSessionService.StoreCartIdForSessionAsync(sessionId, cartId);
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
                        }
                    }
                }
            }
            else
            {
                _logger.LogWarning("OrderService returned error: {StatusCode}, Content: {Content}", 
                    response.StatusCode, content);
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
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> RemoveCartItem(Guid cartId, Guid cartItemId)
    {
        try
        {
            _logger.LogInformation("RemoveCartItem: CartId={CartId}, CartItemId={CartItemId}", cartId, cartItemId);
            
            var client = _httpClientFactory.CreateClient("OrderService");
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Delete, $"/api/order/cart/{cartId}/items/{cartItemId}");
            
            // Forward JWT token to OrderService if present
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
            }
            
            var response = await client.SendAsync(httpRequestMessage);
            var content = await response.Content.ReadAsStringAsync();
            
            _logger.LogInformation("RemoveCartItem: OrderService response - StatusCode={StatusCode}, Content={Content}", 
                response.StatusCode, content);
            
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cart item: {Message}", ex.Message);
            return StatusCode(500, new { error = $"Failed to remove cart item: {ex.Message}" });
        }
    }

    [HttpDelete("cart/{cartId}")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> ClearCart(Guid cartId)
    {
        try
        {
            _logger.LogInformation("ClearCart: CartId={CartId}", cartId);
            
            var client = _httpClientFactory.CreateClient("OrderService");
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Delete, $"/api/order/cart/{cartId}");
            
            // Forward JWT token to OrderService if present
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
            }
            
            var response = await client.SendAsync(httpRequestMessage);
            var content = await response.Content.ReadAsStringAsync();
            
            _logger.LogInformation("ClearCart: OrderService response - StatusCode={StatusCode}, Content={Content}", 
                response.StatusCode, content);
            
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cart: {Message}", ex.Message);
            return StatusCode(500, new { error = $"Failed to clear cart: {ex.Message}" });
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

    // Vendor endpoints
    [HttpGet("vendor/my-restaurants")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> GetMyRestaurants()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("RestaurantService");
            
            // Forward Authorization header
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader))
            {
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authHeader.Replace("Bearer ", ""));
            }
            
            var response = await client.GetAsync("/api/restaurant/vendor/my-restaurants");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }
            
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vendor restaurants");
            return StatusCode(500, new { error = "Failed to fetch vendor restaurants" });
        }
    }

    [HttpPost("vendor/restaurants")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> CreateRestaurant([FromBody] object request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("RestaurantService");
            
            // Forward Authorization header
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader))
            {
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authHeader.Replace("Bearer ", ""));
            }
            
            var json = System.Text.Json.JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/restaurant", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                return Content(responseContent, "application/json");
            }
            
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating restaurant");
            return StatusCode(500, new { error = "Failed to create restaurant" });
        }
    }

    [HttpPut("vendor/restaurants/{restaurantId}")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> UpdateRestaurant(Guid restaurantId, [FromBody] object request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("RestaurantService");
            
            // Forward Authorization header
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader))
            {
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authHeader.Replace("Bearer ", ""));
            }
            
            var json = System.Text.Json.JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PutAsync($"/api/restaurant/{restaurantId}", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                return Content(responseContent, "application/json");
            }
            
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating restaurant");
            return StatusCode(500, new { error = "Failed to update restaurant" });
        }
    }

    [HttpDelete("vendor/restaurants/{restaurantId}")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> DeleteRestaurant(Guid restaurantId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("RestaurantService");
            
            // Forward Authorization header
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader))
            {
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authHeader.Replace("Bearer ", ""));
            }
            
            var response = await client.DeleteAsync($"/api/restaurant/{restaurantId}");
            
            if (response.IsSuccessStatusCode)
            {
                return Ok(new { message = "Restaurant deleted successfully" });
            }
            
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting restaurant");
            return StatusCode(500, new { error = "Failed to delete restaurant" });
        }
    }

    // Menu item management endpoints
    [HttpPost("restaurants/{restaurantId}/menu-items")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> CreateMenuItem(Guid restaurantId, [FromBody] object request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("CatalogService");
            
            // Forward Authorization header
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader))
            {
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authHeader.Replace("Bearer ", ""));
            }
            
            var json = System.Text.Json.JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"/api/catalog/restaurants/{restaurantId}/menu-items", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                return Content(responseContent, "application/json");
            }
            
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating menu item");
            return StatusCode(500, new { error = "Failed to create menu item" });
        }
    }

    [HttpPut("menu-items/{menuItemId}")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> UpdateMenuItem(Guid menuItemId, [FromBody] object request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("CatalogService");
            
            // Forward Authorization header
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader))
            {
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authHeader.Replace("Bearer ", ""));
            }
            
            var json = System.Text.Json.JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PutAsync($"/api/catalog/menu-items/{menuItemId}", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                return Content(responseContent, "application/json");
            }
            
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating menu item");
            return StatusCode(500, new { error = "Failed to update menu item" });
        }
    }

    [HttpPatch("menu-items/{menuItemId}/availability")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> ToggleMenuItemAvailability(Guid menuItemId, [FromBody] object request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("CatalogService");
            
            // Forward Authorization header
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader))
            {
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authHeader.Replace("Bearer ", ""));
            }
            
            var json = System.Text.Json.JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PatchAsync($"/api/catalog/menu-items/{menuItemId}/availability", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                return Content(responseContent, "application/json");
            }
            
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling menu item availability");
            return StatusCode(500, new { error = "Failed to toggle menu item availability" });
        }
    }

    // Admin endpoints
    [HttpGet("admin/restaurants")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllRestaurants([FromQuery] int skip = 0, [FromQuery] int take = 100)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("RestaurantService");
            
            // Forward Authorization header
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader))
            {
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authHeader.Replace("Bearer ", ""));
            }
            
            var response = await client.GetAsync($"/api/restaurant/admin/all?skip={skip}&take={take}");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }
            
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching all restaurants (admin)");
            return StatusCode(500, new { error = "Failed to fetch restaurants" });
        }
    }

    [HttpPatch("admin/restaurants/{restaurantId}/status")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminToggleRestaurantStatus(Guid restaurantId, [FromBody] object request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("RestaurantService");
            
            // Forward Authorization header
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader))
            {
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authHeader.Replace("Bearer ", ""));
            }
            
            var json = System.Text.Json.JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PatchAsync($"/api/restaurant/admin/{restaurantId}/status", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                return Content(responseContent, "application/json");
            }
            
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling restaurant status (admin)");
            return StatusCode(500, new { error = "Failed to toggle restaurant status" });
        }
    }

    [HttpDelete("admin/restaurants/{restaurantId}")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminDeleteRestaurant(Guid restaurantId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("RestaurantService");
            
            // Forward Authorization header
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader))
            {
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authHeader.Replace("Bearer ", ""));
            }
            
            var response = await client.DeleteAsync($"/api/restaurant/admin/{restaurantId}");
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                return Content(responseContent, "application/json");
            }
            
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting restaurant (admin)");
            return StatusCode(500, new { error = "Failed to delete restaurant" });
        }
    }
}

public record CreateCartRequest(Guid? RestaurantId);
public record AddCartItemRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("menuItemId")]
    public Guid MenuItemId { get; init; }
    
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
    
    [System.Text.Json.Serialization.JsonPropertyName("price")]
    public decimal Price { get; init; }
    
    [System.Text.Json.Serialization.JsonPropertyName("quantity")]
    public int Quantity { get; init; }
    
    [System.Text.Json.Serialization.JsonPropertyName("options")]
    public Dictionary<string, string>? Options { get; init; }
}
public record UpdateCartItemRequest(int Quantity);
public record PlaceOrderRequest(
    Guid CartId,
    string DeliveryAddress,
    string? IdempotencyKey
);

public record RegisterRequest(string Email, string? PhoneNumber, string Password, string? Role);
public record LoginRequest(string Email, string Password);
public record RefreshTokenRequest(string RefreshToken);