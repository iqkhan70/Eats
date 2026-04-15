using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using TraditionalEats.BuildingBlocks.Redis;

namespace TraditionalEats.Mobile.Bff.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MobileBffController : ControllerBase
{
    private static readonly Guid CustomRequestMenuItemId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MobileBffController> _logger;
    private readonly ICartSessionService _cartSessionService;
    private readonly IConfiguration _configuration;

    private const string SESSION_HEADER_NAME = "X-Cart-Session-Id";

    public MobileBffController(
        IHttpClientFactory httpClientFactory,
        ILogger<MobileBffController> logger,
        ICartSessionService cartSessionService,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _cartSessionService = cartSessionService;
        _configuration = configuration;
    }

    // ----------------------------
    // Helpers
    // ----------------------------

    private async Task TryDeleteOldAppImageAsync(string? oldPath, string? newPath)
    {
        if (string.IsNullOrWhiteSpace(oldPath) || !oldPath.Contains("appimages", StringComparison.OrdinalIgnoreCase))
            return;
        if (string.Equals(oldPath?.Trim(), newPath?.Trim(), StringComparison.OrdinalIgnoreCase))
            return;
        try
        {
            var client = _httpClientFactory.CreateClient("DocumentService");
            ForwardBearerToken(client);
            var body = new StringContent(JsonSerializer.Serialize(new { path = oldPath }), System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/document/delete-app-image", body);
            if (response.IsSuccessStatusCode)
                _logger.LogInformation("Deleted replaced app image from S3");
            else
                _logger.LogWarning("Delete app image returned {Status}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete replaced app image");
        }
    }

    private async Task TryMergeGuestCartAsync(string loginResponseJson)
    {
        try
        {
            var loginJson = JsonSerializer.Deserialize<JsonElement>(loginResponseJson);
            if (!loginJson.TryGetProperty("accessToken", out var tokenEl))
                return;
            var accessToken = tokenEl.GetString();
            if (string.IsNullOrEmpty(accessToken))
                return;

            var userId = DecodeUserIdFromJwt(accessToken);
            if (!userId.HasValue)
                return;

            var guestSessionId = await GetOrCreateSessionIdAsync();
            var guestCartId = await _cartSessionService.GetCartIdForSessionAsync(guestSessionId);
            if (!guestCartId.HasValue)
                return;

            var userCartId = await _cartSessionService.GetCartIdForUserAsync(userId.Value);
            var orderClient = _httpClientFactory.CreateClient("OrderService");
            var mergeUrl = userCartId.HasValue
                ? $"/api/order/cart/merge?guestCartId={guestCartId.Value}&userCartId={userCartId.Value}"
                : $"/api/order/cart/merge?guestCartId={guestCartId.Value}&userCartId={Guid.Empty}";

            var mergeResponse = await orderClient.PostAsync(mergeUrl, null);
            if (mergeResponse.IsSuccessStatusCode)
            {
                var mergeContent = await mergeResponse.Content.ReadAsStringAsync();
                var mergedCart = JsonSerializer.Deserialize<JsonElement>(mergeContent);
                if (mergedCart.TryGetProperty("cartId", out var mergedCartIdElement))
                {
                    var finalCartId = Guid.Parse(mergedCartIdElement.GetString()!);
                    await _cartSessionService.StoreCartIdForUserAsync(userId.Value, finalCartId);
                    await _cartSessionService.ClearSessionCartAsync(guestSessionId);
                    _logger.LogInformation("Merged guest cart into user {UserId}", userId.Value);
                }
            }
            else
            {
                await _cartSessionService.StoreCartIdForUserAsync(userId.Value, guestCartId.Value);
                await _cartSessionService.ClearSessionCartAsync(guestSessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to merge guest cart after external login");
        }
    }

    private static Guid? DecodeUserIdFromJwt(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length != 3) return null;
            var payload = parts[1];
            var base64 = payload.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4) { case 2: base64 += "=="; break; case 3: base64 += "="; break; }
            var bytes = Convert.FromBase64String(base64);
            var json = System.Text.Encoding.UTF8.GetString(bytes);
            var doc = JsonSerializer.Deserialize<JsonElement>(json);
            JsonElement subEl;
            if (doc.TryGetProperty("sub", out subEl) || doc.TryGetProperty("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", out subEl))
            {
                var sub = subEl.GetString();
                return Guid.TryParse(sub, out var g) ? g : null;
            }
            return null;
        }
        catch { return null; }
    }

    private void ForwardBearerToken(HttpClient client)
    {
        if (Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var raw = authHeader.ToString();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                client.DefaultRequestHeaders.Remove("Authorization");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", raw);
            }
        }
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

    private static ContentResult JsonString(string json, int statusCode = 200)
        => new()
        {
            StatusCode = statusCode,
            ContentType = "application/json",
            Content = json
        };

    // ----------------------------
    // Push notifications
    // ----------------------------

    [HttpPost("notifications/push-tokens")]
    [Authorize]
    public async Task<IActionResult> RegisterPushToken([FromBody] RegisterPushTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PushToken))
            return BadRequest(new { message = "pushToken is required" });

        try
        {
            var client = _httpClientFactory.CreateClient("NotificationService");
            ForwardBearerToken(client);
            var response = await client.PostAsJsonAsync("/api/notification/push-tokens", request);
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(string.IsNullOrWhiteSpace(content) ? "{}" : content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering push token");
            return StatusCode(500, new { message = "Failed to register push token" });
        }
    }

    [HttpDelete("notifications/push-tokens")]
    [Authorize]
    public async Task<IActionResult> UnregisterPushToken([FromBody] UnregisterPushTokenRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("NotificationService");
            ForwardBearerToken(client);

            using var message = new HttpRequestMessage(HttpMethod.Delete, "/api/notification/push-tokens")
            {
                Content = JsonContent.Create(request)
            };

            var response = await client.SendAsync(message);
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(string.IsNullOrWhiteSpace(content) ? "{}" : content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unregistering push token");
            return StatusCode(500, new { message = "Failed to unregister push token" });
        }
    }

    // ----------------------------
    // Geocode ZIP (for restaurant search by zip + radius)
    // ----------------------------

    [HttpGet("geocode-zip")]
    [AllowAnonymous]
    public async Task<IActionResult> GeocodeZip([FromQuery] string zip)
    {
        if (string.IsNullOrWhiteSpace(zip))
            return BadRequest(new { message = "zip is required" });
        var zipClean = zip.Trim();
        if (zipClean.Length < 5)
            return BadRequest(new { message = "zip must be at least 5 digits" });
        var zip5 = zipClean.Length >= 5 ? zipClean[..5] : zipClean;

        try
        {
            var client = _httpClientFactory.CreateClient("RestaurantService");
            var response = await client.GetAsync($"/api/ZipCode/{Uri.EscapeDataString(zip5)}");
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return NotFound(new { message = "ZIP code not found. Add it to the ZipCodeLookup table (same as mental health app)." });
                return StatusCode((int)response.StatusCode, new { message = "ZIP lookup failed" });
            }
            var json = await response.Content.ReadAsStringAsync();
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("latitude", out var latProp) && root.TryGetProperty("longitude", out var lonProp)
                && latProp.TryGetDouble(out var latitude) && lonProp.TryGetDouble(out var longitude))
                return Ok(new { latitude, longitude });
            return NotFound(new { message = "ZIP code not found" });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Geocode zip failed for {Zip}", zip5);
            return StatusCode(500, new { message = "ZIP lookup failed" });
        }
    }

    // ----------------------------
    // Restaurants
    // ----------------------------

    [HttpGet("restaurants")]
    public async Task<IActionResult> GetRestaurants(
        [FromQuery] string? location,
        [FromQuery] string? category,
        [FromQuery] string? cuisineType,
        [FromQuery] Guid? menuCategoryId,
        [FromQuery] double? latitude,
        [FromQuery] double? longitude,
        [FromQuery] double? radiusMiles,
        [FromQuery] string? zip,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("RestaurantService");

            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(location)) queryParams.Add($"location={Uri.EscapeDataString(location)}");
            if (!string.IsNullOrEmpty(category)) queryParams.Add($"vendorType={Uri.EscapeDataString(category)}");
            if (!string.IsNullOrEmpty(cuisineType)) queryParams.Add($"cuisineType={Uri.EscapeDataString(cuisineType)}");
            if (latitude.HasValue) queryParams.Add($"latitude={latitude.Value}");
            if (longitude.HasValue) queryParams.Add($"longitude={longitude.Value}");
            if (radiusMiles.HasValue) queryParams.Add($"radiusMiles={radiusMiles.Value}");
            if (!string.IsNullOrEmpty(zip)) queryParams.Add($"zip={Uri.EscapeDataString(zip)}");
            // When filtering by menu category (CatalogService), fetch a larger set then paginate after filtering.
            var upstreamSkip = menuCategoryId.HasValue ? 0 : skip;
            var upstreamTake = menuCategoryId.HasValue ? 5000 : take;
            queryParams.Add($"skip={upstreamSkip}");
            queryParams.Add($"take={upstreamTake}");

            var queryString = queryParams.Any() ? "?" + string.Join("&", queryParams) : "";
            var response = await client.GetAsync($"/api/restaurant{queryString}");

            var content = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                if (!menuCategoryId.HasValue)
                    return JsonString(content);

                var catalog = _httpClientFactory.CreateClient("CatalogService");
                var idsRes = await catalog.GetAsync($"/api/catalog/categories/{menuCategoryId.Value}/restaurants");
                if (!idsRes.IsSuccessStatusCode)
                    return JsonString("[]", 200);

                var idsJson = await idsRes.Content.ReadAsStringAsync();
                var restaurantIds = JsonSerializer.Deserialize<List<Guid>>(idsJson) ?? new();
                if (restaurantIds.Count == 0)
                    return JsonString("[]", 200);

                var idSet = restaurantIds.ToHashSet();
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    return JsonString("[]", 200);

                var filtered = new List<JsonElement>();
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (TryGetRestaurantId(el, out var rid) && idSet.Contains(rid))
                        filtered.Add(el.Clone());
                }

                var page = filtered.Skip(Math.Max(0, skip)).Take(Math.Max(0, take)).ToList();
                var jsonOut = JsonSerializer.Serialize(page, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

                return JsonString(jsonOut);
            }

            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching restaurants. Ensure RestaurantService is running (e.g. port 5007).");
            return StatusCode(500, new { error = "Failed to fetch restaurants", detail = ex.Message });
        }
    }

    private static bool TryGetRestaurantId(JsonElement el, out Guid restaurantId)
    {
        restaurantId = default;

        if (el.ValueKind != JsonValueKind.Object)
            return false;

        if (el.TryGetProperty("restaurantId", out var p))
        {
            if (p.ValueKind == JsonValueKind.String && Guid.TryParse(p.GetString(), out restaurantId))
                return true;
            if (p.ValueKind == JsonValueKind.Undefined || p.ValueKind == JsonValueKind.Null)
                return false;
        }

        if (el.TryGetProperty("id", out var idp))
        {
            if (idp.ValueKind == JsonValueKind.String && Guid.TryParse(idp.GetString(), out restaurantId))
                return true;
        }

        return false;
    }

    [HttpGet("restaurants/{id}")]
    public async Task<IActionResult> GetRestaurant(Guid id)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("RestaurantService");
            var response = await client.GetAsync($"/api/restaurant/{id}");
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
                return JsonString(content);

            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching restaurant {RestaurantId}", id);
            return StatusCode(500, new { error = "Failed to fetch restaurant" });
        }
    }

    [HttpGet("search-suggestions")]
    public async Task<IActionResult> GetSearchSuggestions([FromQuery] string query, [FromQuery] int maxResults = 10)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("RestaurantService");
            var response = await client.GetAsync($"/api/restaurant/search-suggestions?query={Uri.EscapeDataString(query)}&maxResults={maxResults}");
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
                return JsonString(content);

            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching search suggestions");
            return StatusCode(500, new { error = "Failed to fetch search suggestions" });
        }
    }

    // ----------------------------
    // Catalog
    // ----------------------------

    [HttpGet("restaurants/{restaurantId}/menu")]
    public async Task<IActionResult> GetRestaurantMenu(Guid restaurantId, [FromQuery] Guid? categoryId = null)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("CatalogService");
            var queryString = categoryId.HasValue ? $"?categoryId={categoryId.Value}" : "";
            var response = await client.GetAsync($"/api/catalog/restaurants/{restaurantId}/menu-items{queryString}");
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
                return JsonString(content);

            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching restaurant menu. CatalogService may be unreachable. Returning empty list for RestaurantId={RestaurantId}", restaurantId);
            // Return empty array so app can load restaurant page; avoids 500 when CatalogService is down
            return JsonString("[]", 200);
        }
    }

    [HttpGet("catalog/menu-items/deal-info")]
    [AllowAnonymous]
    public async Task<IActionResult> GetMenuItemDealInfo([FromQuery] string? menuItemIds = null)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("CatalogService");
            var query = !string.IsNullOrWhiteSpace(menuItemIds) ? $"?menuItemIds={Uri.EscapeDataString(menuItemIds)}" : "";
            var response = await client.GetAsync($"/api/catalog/menu-items/deal-info{query}");
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching menu item deal info");
            return StatusCode(500, new { message = "Failed to get deal info" });
        }
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("CatalogService");
            var response = await client.GetAsync("/api/catalog/categories");
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
                return JsonString(content);

            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching categories. CatalogService may be down. Returning empty array.");
            // Return empty array so app can load; avoids 500 when CatalogService/Redis is unavailable (e.g. local dev)
            return JsonString("[]", 200);
        }
    }

    [HttpGet("admin/categories")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminGetCategories()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("CatalogService");
            ForwardBearerToken(client);

            var response = await client.GetAsync("/api/catalog/categories");
            var content = await response.Content.ReadAsStringAsync();

            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching categories (admin)");
            return StatusCode(500, new { error = "Failed to fetch categories" });
        }
    }

    [HttpPost("admin/categories")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminCreateCategory([FromBody] object? request)
    {
        if (request == null)
            return BadRequest(new { message = "Request body is required" });

        try
        {
            var client = _httpClientFactory.CreateClient("CatalogService");
            ForwardBearerToken(client);

            var json = System.Text.Json.JsonSerializer.Serialize(request);
            var body = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/catalog/categories", body);

            var content = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating category (admin)");
            return StatusCode(500, new { error = "Failed to create category" });
        }
    }

    [HttpPut("admin/categories/{categoryId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminUpdateCategory(Guid categoryId, [FromBody] object? request)
    {
        if (request == null)
            return BadRequest(new { message = "Request body is required" });

        try
        {
            var client = _httpClientFactory.CreateClient("CatalogService");
            ForwardBearerToken(client);

            var json = System.Text.Json.JsonSerializer.Serialize(request);
            var body = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PutAsync($"/api/catalog/categories/{categoryId}", body);

            var content = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating category (admin)");
            return StatusCode(500, new { error = "Failed to update category" });
        }
    }

    [HttpDelete("admin/categories/{categoryId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminDeleteCategory(Guid categoryId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("CatalogService");
            ForwardBearerToken(client);

            var response = await client.DeleteAsync($"/api/catalog/categories/{categoryId}");
            var content = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting category (admin)");
            return StatusCode(500, new { error = "Failed to delete category" });
        }
    }

    // ----------------------------
    // Orders
    // ----------------------------

    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("OrderService");
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/order");

            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
                httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());

            var response = await client.SendAsync(httpRequestMessage);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
                return JsonString(content);

            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching orders");
            return StatusCode(500, new { error = "Failed to fetch orders" });
        }
    }

    [HttpPost("orders/place")]
    public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderRequest request)
    {
        try
        {
            // Check vendor Stripe status BEFORE creating the order
            // Get restaurantId from cart first
            Guid restaurantId = default;
            try
            {
                var cartClient = _httpClientFactory.CreateClient("OrderService");
                var cartRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/order/cart/{request.CartId}");
                if (Request.Headers.TryGetValue("Authorization", out var cartAuthHeader))
                    cartRequest.Headers.TryAddWithoutValidation("Authorization", cartAuthHeader.ToString());

                var cartResponse = await cartClient.SendAsync(cartRequest);
                if (cartResponse.IsSuccessStatusCode)
                {
                    var cartContent = await cartResponse.Content.ReadAsStringAsync();
                    var cartDoc = System.Text.Json.JsonDocument.Parse(cartContent);
                    var root = cartDoc.RootElement;
                    var restEl = root.TryGetProperty("restaurantId", out var r) ? r : root.GetProperty("RestaurantId");
                    if (restEl.ValueKind != System.Text.Json.JsonValueKind.Null && restEl.ValueKind != System.Text.Json.JsonValueKind.Undefined)
                        restaurantId = Guid.Parse(restEl.GetString()!);

                    _logger.LogInformation("PlaceOrder: Cart {CartId} has restaurantId={RestaurantId}", request.CartId, restaurantId);
                }
                else
                {
                    _logger.LogWarning("PlaceOrder: Could not fetch cart {CartId}, status={Status}", request.CartId, cartResponse.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PlaceOrder: Could not fetch cart to check vendor Stripe status");
            }

            // If we have restaurantId, check vendor Stripe status before creating order
            if (restaurantId != default)
            {
                try
                {
                    var checkPaymentClient = _httpClientFactory.CreateClient("PaymentService");
                    var checkRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/payment/restaurant/{restaurantId}/payment-ready");
                    var checkResponse = await checkPaymentClient.SendAsync(checkRequest);

                    if (checkResponse.IsSuccessStatusCode)
                    {
                        var checkContent = await checkResponse.Content.ReadAsStringAsync();
                        var checkDoc = System.Text.Json.JsonDocument.Parse(checkContent);
                        var paymentReady = checkDoc.RootElement.TryGetProperty("paymentReady", out var pr) && pr.GetBoolean();

                        _logger.LogInformation("PlaceOrder: Restaurant {RestaurantId} payment ready check: {PaymentReady}", restaurantId, paymentReady);

                        if (!paymentReady)
                        {
                            _logger.LogWarning("PlaceOrder: Vendor Stripe not set up for restaurant {RestaurantId}, blocking order creation", restaurantId);
                            return BadRequest(new { error = "This vendor is not set up to accept payments yet. Please contact the vendor directly or try again later." });
                        }
                    }
                    else
                    {
                        _logger.LogWarning("PlaceOrder: Payment readiness check failed for restaurant {RestaurantId}, status={Status}", restaurantId, checkResponse.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "PlaceOrder: Could not validate vendor Stripe status, proceeding with order creation");
                    // Don't block order creation if validation fails - let it fail later during checkout
                }
            }
            else
            {
                _logger.LogWarning("PlaceOrder: restaurantId is default (empty), skipping payment readiness check. CartId={CartId}", request.CartId);
            }

            var orderClient = _httpClientFactory.CreateClient("OrderService");
            var orderRequest = new HttpRequestMessage(HttpMethod.Post, "/api/order/place")
            {
                Content = JsonContent.Create(request)
            };
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
                orderRequest.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());

            var orderResponse = await orderClient.SendAsync(orderRequest);
            var orderContent = await orderResponse.Content.ReadAsStringAsync();
            if (!orderResponse.IsSuccessStatusCode)
                return StatusCode((int)orderResponse.StatusCode, orderContent);

            Guid orderId;
            try
            {
                var orderJson = System.Text.Json.JsonDocument.Parse(orderContent);
                var orderIdEl = orderJson.RootElement.TryGetProperty("orderId", out var o) ? o : orderJson.RootElement.GetProperty("OrderId");
                orderId = Guid.Parse(orderIdEl.GetString()!);
            }
            catch
            {
                return StatusCode((int)orderResponse.StatusCode, orderContent);
            }

            var getOrderRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/order/{orderId}");
            if (Request.Headers.TryGetValue("Authorization", out authHeader))
                getOrderRequest.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
            var getOrderResponse = await orderClient.SendAsync(getOrderRequest);
            if (!getOrderResponse.IsSuccessStatusCode)
            {
                return Ok(new { orderId, checkoutUrl = (string?)null, error = "Order placed but could not load details for payment" });
            }

            var orderDetailContent = await getOrderResponse.Content.ReadAsStringAsync();
            decimal total = 0, serviceFee = 0;
            // Reuse restaurantId from earlier check, or get it from order if not set
            try
            {
                var orderDoc = System.Text.Json.JsonDocument.Parse(orderDetailContent);
                var root = orderDoc.RootElement;
                total = root.TryGetProperty("total", out var t) ? t.GetDecimal() : root.GetProperty("Total").GetDecimal();
                serviceFee = root.TryGetProperty("serviceFee", out var s) ? s.GetDecimal() : (root.TryGetProperty("ServiceFee", out s) ? s.GetDecimal() : 0);
                if (restaurantId == default)
                {
                    var restEl = root.TryGetProperty("restaurantId", out var r) ? r : root.GetProperty("RestaurantId");
                    if (restEl.ValueKind != System.Text.Json.JsonValueKind.Null && restEl.ValueKind != System.Text.Json.JsonValueKind.Undefined)
                        restaurantId = Guid.Parse(restEl.GetString()!);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PlaceOrder: Could not parse order details for checkout");
                return Ok(new { orderId, checkoutUrl = (string?)null, error = "Order placed but could not initiate payment" });
            }

            var baseUrl = _configuration["AppBaseUrl"] ?? (Request.Scheme + "://" + Request.Host);
            var successUrl = !string.IsNullOrWhiteSpace(request.SuccessUrl)
                ? request.SuccessUrl!.Trim()
                : baseUrl.TrimEnd('/') + "/orders?payment=success";
            var cancelUrl = !string.IsNullOrWhiteSpace(request.CancelUrl)
                ? request.CancelUrl!.Trim()
                : baseUrl.TrimEnd('/') + "/cart?payment=cancelled";
            // Append orderId to cancel URL so app can auto-cancel when user abandons Stripe checkout
            if (cancelUrl.Contains("payment-done", StringComparison.OrdinalIgnoreCase) || cancelUrl.Contains("status=cancelled", StringComparison.OrdinalIgnoreCase))
                cancelUrl += (cancelUrl.Contains("?") ? "&" : "?") + "orderId=" + orderId.ToString();
            var checkoutPaymentClient = _httpClientFactory.CreateClient("PaymentService");
            var checkoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/payment/checkout/session");
            if (Request.Headers.TryGetValue("Authorization", out var checkoutAuthHeader))
                checkoutRequest.Headers.TryAddWithoutValidation("Authorization", checkoutAuthHeader.ToString());
            checkoutRequest.Content = JsonContent.Create(new
            {
                orderId,
                amount = total,
                serviceFee,
                restaurantId,
                successUrl,
                cancelUrl
            });

            var checkoutResponse = await checkoutPaymentClient.SendAsync(checkoutRequest);
            var checkoutContent = await checkoutResponse.Content.ReadAsStringAsync();
            if (checkoutResponse.IsSuccessStatusCode)
            {
                var checkoutDoc = System.Text.Json.JsonDocument.Parse(checkoutContent);
                var url = checkoutDoc.RootElement.TryGetProperty("url", out var u) ? u.GetString() : null;
                return Ok(new { orderId, checkoutUrl = url });
            }

            // Extract user-friendly error message from response
            string? errorMessage = null;
            if (checkoutResponse.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                try
                {
                    var errorDoc = System.Text.Json.JsonDocument.Parse(checkoutContent);
                    if (errorDoc.RootElement.TryGetProperty("message", out var msgEl))
                        errorMessage = msgEl.GetString();
                    else if (errorDoc.RootElement.TryGetProperty("error", out var errEl))
                        errorMessage = errEl.GetString();
                }
                catch
                {
                    // If parsing fails, use the raw content (truncated)
                    errorMessage = checkoutContent.Length > 200 ? checkoutContent.Substring(0, 200) + "..." : checkoutContent;
                }
            }

            // Default user-friendly message if no specific error was extracted
            errorMessage ??= "This vendor is not set up to accept payments yet. Your order has been placed, but payment cannot be processed. Please contact the vendor directly.";

            _logger.LogWarning("PlaceOrder: Checkout session failed for order {OrderId}: {Content}", orderId, checkoutContent);
            return Ok(new { orderId, checkoutUrl = (string?)null, error = errorMessage });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error placing order");
            return StatusCode(500, new { error = "Failed to place order" });
        }
    }

    [HttpGet("payments/vendor/onboarding-status")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> GetVendorStripeOnboardingStatus()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PaymentService");
            ForwardBearerToken(client);
            var response = await client.GetAsync("/api/payment/vendor/onboarding-status");
            var content = await response.Content.ReadAsStringAsync();
            return new ContentResult { Content = content, ContentType = "application/json", StatusCode = (int)response.StatusCode };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vendor Stripe onboarding status");
            return StatusCode(500, new { error = "Failed to fetch onboarding status" });
        }
    }

    [HttpPost("payments/vendor/connect-link")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> CreateVendorStripeConnectLink()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PaymentService");
            ForwardBearerToken(client);
            var response = await client.PostAsync("/api/payment/vendor/connect-link", null);
            var content = await response.Content.ReadAsStringAsync();
            return new ContentResult { Content = content, ContentType = "application/json", StatusCode = (int)response.StatusCode };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating vendor Stripe connect link");
            return StatusCode(500, new { error = "Failed to create Stripe connect link" });
        }
    }

    [HttpPost("payments/vendor/refresh-onboarding-status")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> RefreshVendorStripeOnboardingStatus()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PaymentService");
            ForwardBearerToken(client);
            var response = await client.PostAsync("/api/payment/vendor/refresh-onboarding-status", null);
            var content = await response.Content.ReadAsStringAsync();
            return new ContentResult { Content = content, ContentType = "application/json", StatusCode = (int)response.StatusCode };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing vendor Stripe onboarding status");
            return StatusCode(500, new { error = "Failed to refresh onboarding status" });
        }
    }

    [HttpGet("payments/restaurant/{restaurantId}/payment-ready")]
    [AllowAnonymous]
    public async Task<IActionResult> CheckRestaurantPaymentReady(Guid restaurantId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PaymentService");
            var response = await client.GetAsync($"/api/payment/restaurant/{restaurantId}/payment-ready");
            var content = await response.Content.ReadAsStringAsync();
            return new ContentResult { Content = content, ContentType = "application/json", StatusCode = (int)response.StatusCode };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking restaurant payment readiness");
            return StatusCode(500, new { error = "Failed to check payment readiness" });
        }
    }

    [HttpGet("orders/{orderId}")]
    public async Task<IActionResult> GetOrder(Guid orderId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("OrderService");
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/order/{orderId}");

            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
                httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());

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

    [HttpPost("orders/{orderId}/retry-payment")]
    [Authorize]
    public async Task<IActionResult> RetryPayment(Guid orderId, [FromBody] RetryPaymentRequest? body = null)
    {
        try
        {
            var orderClient = _httpClientFactory.CreateClient("OrderService");
            var getOrderRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/order/{orderId}");
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
                getOrderRequest.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());

            var getOrderResponse = await orderClient.SendAsync(getOrderRequest);
            var orderContent = await getOrderResponse.Content.ReadAsStringAsync();
            if (!getOrderResponse.IsSuccessStatusCode)
                return StatusCode((int)getOrderResponse.StatusCode, orderContent);

            decimal total = 0, serviceFee = 0;
            Guid restaurantId = default;
            try
            {
                using var orderDoc = System.Text.Json.JsonDocument.Parse(orderContent);
                var root = orderDoc.RootElement;
                total = root.TryGetProperty("total", out var t) ? t.GetDecimal() : root.GetProperty("Total").GetDecimal();
                serviceFee = root.TryGetProperty("serviceFee", out var s) ? s.GetDecimal() : (root.TryGetProperty("ServiceFee", out s) ? s.GetDecimal() : 0);
                var restEl = root.TryGetProperty("restaurantId", out var r) ? r : root.GetProperty("RestaurantId");
                if (restEl.ValueKind != System.Text.Json.JsonValueKind.Null && restEl.ValueKind != System.Text.Json.JsonValueKind.Undefined)
                    restaurantId = Guid.Parse(restEl.GetString()!);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RetryPayment: Could not parse order details for checkout");
                return StatusCode(500, new { error = "Could not load order details for payment" });
            }

            if (restaurantId == default)
                return BadRequest(new { error = "Order is missing restaurantId" });

            var baseUrl = _configuration["AppBaseUrl"] ?? (Request.Scheme + "://" + Request.Host);
            var successUrl = !string.IsNullOrWhiteSpace(body?.SuccessUrl)
                ? body.SuccessUrl!.Trim()
                : baseUrl.TrimEnd('/') + "/orders?payment=success";
            var cancelUrl = !string.IsNullOrWhiteSpace(body?.CancelUrl)
                ? body.CancelUrl!.Trim()
                : baseUrl.TrimEnd('/') + "/orders/" + orderId + "?payment=cancelled";
            if (cancelUrl.Contains("payment-done", StringComparison.OrdinalIgnoreCase) || cancelUrl.Contains("status=cancelled", StringComparison.OrdinalIgnoreCase))
                cancelUrl += (cancelUrl.Contains("?") ? "&" : "?") + "orderId=" + orderId.ToString();

            var checkoutPaymentClient = _httpClientFactory.CreateClient("PaymentService");
            var checkoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/payment/checkout/session");
            if (Request.Headers.TryGetValue("Authorization", out var checkoutAuthHeader))
                checkoutRequest.Headers.TryAddWithoutValidation("Authorization", checkoutAuthHeader.ToString());
            checkoutRequest.Content = JsonContent.Create(new
            {
                orderId,
                amount = total,
                serviceFee,
                restaurantId,
                successUrl,
                cancelUrl
            });

            var checkoutResponse = await checkoutPaymentClient.SendAsync(checkoutRequest);
            var checkoutContent = await checkoutResponse.Content.ReadAsStringAsync();
            if (!checkoutResponse.IsSuccessStatusCode)
                return StatusCode((int)checkoutResponse.StatusCode, checkoutContent);

            var checkoutDoc = System.Text.Json.JsonDocument.Parse(checkoutContent);
            var url = checkoutDoc.RootElement.TryGetProperty("url", out var u) ? u.GetString() : null;
            return Ok(new { orderId, checkoutUrl = url });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RetryPayment failed for order {OrderId}", orderId);
            return StatusCode(500, new { error = "Failed to retry payment" });
        }
    }

    [HttpPost("orders/{orderId}/cancel")]
    [Authorize]
    public async Task<IActionResult> CancelOrder(Guid orderId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("OrderService");
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/order/{orderId}/cancel");

            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
                httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());

            var response = await client.SendAsync(httpRequestMessage);
            var content = await response.Content.ReadAsStringAsync();

            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling order {OrderId}", orderId);
            return StatusCode(500, new { error = "Failed to cancel order" });
        }
    }

    [HttpPost("orders/{orderId}/refund")]
    [Authorize(Roles = "Vendor,Staff,Admin")]
    public async Task<IActionResult> RefundOrder(Guid orderId)
    {
        try
        {
            var paymentClient = _httpClientFactory.CreateClient("PaymentService");
            ForwardBearerToken(paymentClient);

            var response = await paymentClient.PostAsJsonAsync("/api/payment/refund-by-order", new { orderId });
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                // Update order status to Refunded so UI hides Refund button (PaymentService may also do this; BFF ensures it happens)
                try
                {
                    var orderClient = _httpClientFactory.CreateClient("OrderService");
                    ForwardBearerToken(orderClient);
                    await orderClient.PutAsJsonAsync($"/api/order/{orderId}/status", new { Status = "Refunded", Notes = "Refunded by vendor" });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "RefundOrder: Failed to update order status to Refunded for OrderId={OrderId}", orderId);
                }
            }

            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refunding order {OrderId}", orderId);
            return StatusCode(500, new { error = "Failed to refund order" });
        }
    }

    // ----------------------------
    // Auth
    // ----------------------------

    [HttpPost("auth/register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("IdentityService");
            var response = await client.PostAsJsonAsync("/api/auth/register", request);

            var content = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
                return JsonString(content);

            return StatusCode((int)response.StatusCode, content);
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
            // Guest session/cart for merge
            var guestSessionId = await GetOrCreateSessionIdAsync();
            var guestCartId = await _cartSessionService.GetCartIdForSessionAsync(guestSessionId);

            var client = _httpClientFactory.CreateClient("IdentityService");
            var response = await client.PostAsJsonAsync("/api/auth/login", request);

            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                // Attempt merge
                try
                {
                    var loginJson = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(content);

                    Guid? resolvedUserId = null;
                    if (loginJson.TryGetProperty("userId", out var userIdElement) &&
                        Guid.TryParse(userIdElement.GetString(), out var uidFromProp))
                        resolvedUserId = uidFromProp;
                    else if (loginJson.TryGetProperty("user", out var userEl) && userEl.TryGetProperty("id", out var idEl) &&
                             Guid.TryParse(idEl.GetString(), out var uidFromUser))
                        resolvedUserId = uidFromUser;
                    else if (loginJson.TryGetProperty("id", out var idEl2) &&
                             Guid.TryParse(idEl2.GetString(), out var uidFromId))
                        resolvedUserId = uidFromId;

                    // Password login typically returns only tokens; user id is in JWT "sub".
                    if (resolvedUserId == null && loginJson.TryGetProperty("accessToken", out var accessTokenEl))
                    {
                        var jwt = accessTokenEl.GetString();
                        if (!string.IsNullOrEmpty(jwt))
                            resolvedUserId = DecodeUserIdFromJwt(jwt);
                    }

                    if (resolvedUserId.HasValue && guestCartId.HasValue)
                    {
                        var userId = resolvedUserId.Value;
                        _logger.LogInformation("Merging guest cart {GuestCartId} into user cart for user {UserId}",
                            guestCartId.Value, userId);

                        var userCartId = await _cartSessionService.GetCartIdForUserAsync(userId);

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
                            await _cartSessionService.StoreCartIdForUserAsync(userId, guestCartId.Value);
                            await _cartSessionService.ClearSessionCartAsync(guestSessionId);
                            _logger.LogWarning("Cart merge failed; transferred guest cart to user");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse login response or merge carts; continuing with login");
                }

                return JsonString(content);
            }

            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return StatusCode(500, new { error = "Failed to login" });
        }
    }

    [HttpPost("auth/google")]
    public async Task<IActionResult> LoginWithGoogle([FromBody] GoogleLoginRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.IdToken))
            return BadRequest(new { message = "ID token is required" });
        try
        {
            var client = _httpClientFactory.CreateClient("IdentityService");
            var response = await client.PostAsJsonAsync("/api/auth/google", request);
            var content = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                await TryMergeGuestCartAsync(content);
                return JsonString(content);
            }
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Google login");
            return StatusCode(500, new { error = "Failed to sign in with Google" });
        }
    }

    /// <summary>
    /// OAuth 2.0 redirect URI for Android when using the Web client with an https:// callback (no custom URI scheme).
    /// Register this exact URL on the Google Cloud Web OAuth client. The in-app browser lands here with ?code=; Expo closes the session when the URL matches.
    /// </summary>
    [HttpGet("oauth/google-callback")]
    [AllowAnonymous]
    public IActionResult GoogleOAuthBrowserCallback()
    {
        const string html = """
<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Sign in</title>
  <style>
    body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; padding: 24px; color: #111827; }
    a { color: #0d99ff; }
    .muted { color: #6b7280; }
    button {
      appearance: none;
      border: 0;
      border-radius: 10px;
      padding: 12px 16px;
      background: #0d99ff;
      color: white;
      font: inherit;
      font-weight: 600;
    }
  </style>
</head>
<body>
  <p>Finishing sign-in and returning to the app…</p>
  <div id="manual" style="display:none;">
    <p class="muted">If Kram does not open automatically, continue below.</p>
    <button id="fallback" type="button">Continue to Kram</button>
  </div>
  <script>
    (function () {
      var appUrl = "com.kram.mobile:/login" + window.location.search + window.location.hash;
      var openApp = function () {
        window.location.href = appUrl;
      };
      try {
        openApp();
      } catch (e) {
        // ignore and show manual link below
      }
      window.setTimeout(function () {
        var fallback = document.getElementById("fallback");
        var manual = document.getElementById("manual");
        if (fallback) fallback.onclick = openApp;
        if (manual) manual.style.display = "block";
      }, 1200);
    })();
  </script>
</body>
</html>
""";
        return Content(html, "text/html", System.Text.Encoding.UTF8);
    }

    [HttpPost("auth/apple")]
    public async Task<IActionResult> LoginWithApple([FromBody] AppleLoginRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.IdToken))
            return BadRequest(new { message = "ID token is required" });
        try
        {
            var client = _httpClientFactory.CreateClient("IdentityService");
            var response = await client.PostAsJsonAsync("/api/auth/apple", request);
            var content = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                await TryMergeGuestCartAsync(content);
                return JsonString(content);
            }
            return new ContentResult { StatusCode = (int)response.StatusCode, Content = content, ContentType = "application/json" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Apple login");
            return StatusCode(500, new { error = "Failed to sign in with Apple" });
        }
    }

    [HttpPost("auth/refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("IdentityService");
            var response = await client.PostAsJsonAsync("/api/auth/refresh", request);

            var content = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
                return JsonString(content);

            return StatusCode((int)response.StatusCode, content);
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

            var content = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
                return JsonString(content);

            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, new { error = "Failed to logout" });
        }
    }

    [HttpDelete("auth/account")]
    [Authorize]
    public async Task<IActionResult> DeleteAccount()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("IdentityService");
            var token = Request.Headers["Authorization"].ToString();
            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", token);

            var response = await client.DeleteAsync("/api/auth/account");
            var content = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
                return JsonString(content);

            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting account");
            return StatusCode(500, new { error = "Failed to delete account" });
        }
    }

    [HttpPost("auth/forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { success = false, message = "Email is required." });
        try
        {
            var client = _httpClientFactory.CreateClient("IdentityService");
            var response = await client.PostAsJsonAsync("/api/auth/forgot-password", request);
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(content, (int)response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Forgot password: IdentityService unreachable. Ensure it is running (e.g. port 5000).");
            return StatusCode(503, new { success = false, message = "Authentication service is temporarily unavailable. Please try again later." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during forgot password");
            return StatusCode(500, new { success = false, message = "Failed to process request. Please try again later." });
        }
    }

    [HttpPost("auth/vendor-request")]
    [Authorize]
    public async Task<IActionResult> CreateVendorRequest()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("IdentityService");
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/vendor-request");
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
                request.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Create vendor request failed");
            return StatusCode(500, new { message = "Failed to submit request" });
        }
    }

    [HttpGet("auth/vendor-request/status")]
    [Authorize]
    public async Task<IActionResult> GetVendorRequestStatus()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("IdentityService");
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/vendor-request/status");
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
                request.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get vendor request status failed");
            return StatusCode(500, new { message = "Failed to get status" });
        }
    }

    [HttpGet("admin/vendor-approvals")]
    [Authorize(Roles = "Admin,Coordinator")]
    public async Task<IActionResult> GetPendingVendorApprovals()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("IdentityService");
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/vendor-approvals");
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
                request.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get pending vendor approvals failed");
            return StatusCode(500, new { message = "Failed to get approvals" });
        }
    }

    [HttpPost("admin/vendor-approvals/{requestId:guid}/approve")]
    [Authorize(Roles = "Admin,Coordinator")]
    public async Task<IActionResult> ApproveVendorRequest(Guid requestId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("IdentityService");
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/auth/vendor-approvals/{requestId}/approve");
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
                request.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Approve vendor request failed");
            return StatusCode(500, new { message = "Failed to approve request" });
        }
    }

    [HttpPost("admin/sync-users-to-customers")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SyncUsersToCustomers()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("IdentityService");
            ForwardBearerToken(client);

            var response = await client.PostAsync("/api/auth/admin/sync-users-to-customers", null);
            var content = await response.Content.ReadAsStringAsync();

            return JsonString(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync users to customers failed");
            return StatusCode(500, new { message = "Failed to sync" });
        }
    }

    [HttpGet("admin/users/{email}/roles")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetUserRoles(string email)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("IdentityService");
            ForwardBearerToken(client);
            var response = await client.GetAsync($"/api/auth/user-roles/{Uri.EscapeDataString(email)}");
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get user roles failed");
            return StatusCode(500, new { message = "Failed to get user roles" });
        }
    }

    [HttpPost("admin/users/assign-role")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignUserRole([FromBody] AssignRoleRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("IdentityService");
            ForwardBearerToken(client);
            var response = await client.PostAsJsonAsync("/api/auth/assign-role", request);
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Assign role failed");
            return StatusCode(500, new { message = "Failed to assign role" });
        }
    }

    [HttpPost("admin/users/revoke-role")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RevokeUserRole([FromBody] RevokeRoleRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("IdentityService");
            ForwardBearerToken(client);
            var response = await client.PostAsJsonAsync("/api/auth/revoke-role", request);
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Revoke role failed");
            return StatusCode(500, new { message = "Failed to revoke role" });
        }
    }

    [HttpPost("auth/reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(new { success = false, message = "Email, token, and new password are required." });
        try
        {
            var client = _httpClientFactory.CreateClient("IdentityService");
            var response = await client.PostAsJsonAsync("/api/auth/reset-password", request);
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(content, (int)response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Reset password: IdentityService unreachable. Ensure it is running (e.g. port 5000).");
            return StatusCode(503, new { success = false, message = "Authentication service is temporarily unavailable. Please try again later." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during reset password");
            return StatusCode(500, new { success = false, message = "Failed to process request. Please try again later." });
        }
    }

    // ----------------------------
    // Customer profile (proxies to CustomerService)
    // ----------------------------

    [HttpGet("customer/me")]
    [Authorize]
    public async Task<IActionResult> GetCustomerProfile()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("CustomerService");
            ForwardBearerToken(client);
            var response = await client.GetAsync("/api/customer/me");
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get customer profile failed");
            return StatusCode(500, new { message = "Failed to get profile" });
        }
    }

    [HttpPatch("customer/me")]
    [Authorize]
    public async Task<IActionResult> UpdateCustomerProfile([FromBody] JsonElement body)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("CustomerService");
            ForwardBearerToken(client);
            var response = await client.PatchAsJsonAsync("/api/customer/me", body);
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update customer profile failed");
            return StatusCode(500, new { message = "Failed to update profile" });
        }
    }

    [HttpGet("customer/addresses")]
    [Authorize]
    public async Task<IActionResult> GetCustomerAddresses()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("CustomerService");
            ForwardBearerToken(client);
            var response = await client.GetAsync("/api/customer/addresses");
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get addresses failed");
            return StatusCode(500, new { message = "Failed to get addresses" });
        }
    }

    [HttpPost("customer/addresses")]
    [Authorize]
    public async Task<IActionResult> AddCustomerAddress([FromBody] JsonElement body)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("CustomerService");
            ForwardBearerToken(client);
            var response = await client.PostAsJsonAsync("/api/customer/addresses", body);
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Add address failed");
            return StatusCode(500, new { message = "Failed to add address" });
        }
    }

    [HttpPut("customer/addresses/{addressId:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateCustomerAddress(Guid addressId, [FromBody] JsonElement body)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("CustomerService");
            ForwardBearerToken(client);
            var response = await client.PutAsJsonAsync($"/api/customer/addresses/{addressId}", body);
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update address failed");
            return StatusCode(500, new { message = "Failed to update address" });
        }
    }

    [HttpDelete("customer/addresses/{addressId:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteCustomerAddress(Guid addressId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("CustomerService");
            ForwardBearerToken(client);
            var response = await client.DeleteAsync($"/api/customer/addresses/{addressId}");
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete address failed");
            return StatusCode(500, new { message = "Failed to delete address" });
        }
    }

    // ----------------------------
    // Cart
    // ----------------------------

    [HttpPost("cart")]
    [AllowAnonymous]
    public async Task<IActionResult> CreateCart([FromBody] CreateCartRequest? request = null)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("OrderService");
            var requestBody = request ?? new CreateCartRequest(null);

            // Serialize camelCase to match OrderService settings
            var jsonOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/order/cart")
            {
                Content = System.Net.Http.Json.JsonContent.Create(requestBody, options: jsonOptions)
            };

            _logger.LogInformation("CreateCart: Forwarding to OrderService - URL={Url}, RestaurantId={RestaurantId}",
                httpRequestMessage.RequestUri, request?.RestaurantId);

            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
                httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());

            var response = await client.SendAsync(httpRequestMessage);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var result = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(content);
                    if (result.TryGetProperty("cartId", out var cartIdElement) &&
                        Guid.TryParse(cartIdElement.GetString(), out var cartId))
                    {
                        if (User.Identity?.IsAuthenticated == true)
                        {
                            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                            if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
                                await _cartSessionService.StoreCartIdForUserAsync(userId, cartId);
                        }
                        else
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

            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating cart: {Message}", ex.Message);
            return StatusCode(500, new { error = $"Failed to create cart: {ex.Message}", details = ex.ToString() });
        }
    }

    [HttpGet("cart")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCart()
    {
        try
        {
            // Auth user
            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
                {
                    var userCartId = await _cartSessionService.GetCartIdForUserAsync(userId);
                    if (userCartId.HasValue)
                    {
                        var client = _httpClientFactory.CreateClient("OrderService");
                        var response = await client.GetAsync($"/api/order/cart/{userCartId.Value}");
                        var content = await response.Content.ReadAsStringAsync();

                        if (response.IsSuccessStatusCode)
                            return JsonString(content);
                    }
                }

                // fallback to OrderService /api/order/cart (by customerId via token)
                var client2 = _httpClientFactory.CreateClient("OrderService");
                var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/order/cart");
                if (Request.Headers.TryGetValue("Authorization", out var authHeader))
                    httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());

                var response2 = await client2.SendAsync(httpRequestMessage);
                var content2 = await response2.Content.ReadAsStringAsync();

                if (response2.IsSuccessStatusCode)
                {
                    // cache cartId
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

                    return JsonString(content2);
                }
                else if (response2.StatusCode == System.Net.HttpStatusCode.NotFound ||
                         response2.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    return Ok((object?)null);
                }

                return StatusCode((int)response2.StatusCode, content2);
            }

            // Guest user
            var sessionId = await GetOrCreateSessionIdAsync();
            var guestCartId = await _cartSessionService.GetCartIdForSessionAsync(sessionId);

            if (guestCartId.HasValue)
            {
                var client = _httpClientFactory.CreateClient("OrderService");
                var response = await client.GetAsync($"/api/order/cart/{guestCartId.Value}");
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                    return JsonString(content);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    await _cartSessionService.ClearSessionCartAsync(sessionId);
                    return Ok((object?)null);
                }
            }

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
    [AllowAnonymous]
    public async Task<IActionResult> AddItemToCart(Guid cartId, [FromBody] AddCartItemRequest? request)
    {
        try
        {
            if (request == null)
                return BadRequest(new { error = "Request body is required and must be valid JSON" });

            var effectiveMenuItemId = request.MenuItemId;
            if (request.IsCustomRequest)
            {
                // Custom request items are not tied to catalog menu items.
                // We map empty Guid to a stable synthetic id for cart storage.
                if (effectiveMenuItemId == Guid.Empty)
                    effectiveMenuItemId = CustomRequestMenuItemId;
            }
            else if (effectiveMenuItemId == Guid.Empty)
            {
                return BadRequest(new { error = "MenuItemId is required and must be a valid GUID" });
            }

            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { error = "Name is required" });

            if (request.Price < 0)
                return BadRequest(new { error = "Price must be non-negative" });

            if (request.Quantity <= 0)
                return BadRequest(new { error = "Quantity must be positive" });

            _logger.LogInformation("AddItemToCart: CartId={CartId}, MenuItemId={MenuItemId}, EffectiveMenuItemId={EffectiveMenuItemId}, IsCustomRequest={IsCustomRequest}, Name={Name}, Price={Price}, Quantity={Quantity}",
                cartId, request.MenuItemId, effectiveMenuItemId, request.IsCustomRequest, request.Name, request.Price, request.Quantity);

            var client = _httpClientFactory.CreateClient("OrderService");

            var orderServiceRequest = new
            {
                MenuItemId = effectiveMenuItemId,
                Name = request.Name,
                Price = request.Price,
                Quantity = request.Quantity,
                Options = request.Options
            };

            var jsonOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/order/cart/{cartId}/items")
            {
                Content = System.Net.Http.Json.JsonContent.Create(orderServiceRequest, options: jsonOptions)
            };

            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
                httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());

            var requestBody = System.Text.Json.JsonSerializer.Serialize(orderServiceRequest, jsonOptions);
            _logger.LogInformation("AddItemToCart: Forwarding to OrderService - URL={Url}, Body={Body}",
                httpRequestMessage.RequestUri, requestBody);

            var response = await client.SendAsync(httpRequestMessage);
            var content = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("AddItemToCart: OrderService response - StatusCode={StatusCode}, Content={Content}",
                response.StatusCode, content);

            if (response.IsSuccessStatusCode)
            {
                if (User.Identity?.IsAuthenticated != true)
                {
                    var sessionId = await GetOrCreateSessionIdAsync();
                    var existingCartId = await _cartSessionService.GetCartIdForSessionAsync(sessionId);
                    if (!existingCartId.HasValue || existingCartId.Value != cartId)
                        await _cartSessionService.StoreCartIdForSessionAsync(sessionId, cartId);
                }
                else
                {
                    var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
                    {
                        var existingCartId = await _cartSessionService.GetCartIdForUserAsync(userId);
                        if (!existingCartId.HasValue || existingCartId.Value != cartId)
                            await _cartSessionService.StoreCartIdForUserAsync(userId, cartId);
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
            return StatusCode(500, new { error = $"Failed to add item to cart: {ex.Message}", details = ex.ToString() });
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
                Content = JsonContent.Create(request)
            };

            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
                httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());

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
    [AllowAnonymous]
    public async Task<IActionResult> RemoveCartItem(Guid cartId, Guid cartItemId)
    {
        try
        {
            _logger.LogInformation("RemoveCartItem: CartId={CartId}, CartItemId={CartItemId}", cartId, cartItemId);

            var client = _httpClientFactory.CreateClient("OrderService");
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Delete, $"/api/order/cart/{cartId}/items/{cartItemId}");

            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
                httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());

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
    [AllowAnonymous]
    public async Task<IActionResult> ClearCart(Guid cartId)
    {
        try
        {
            _logger.LogInformation("ClearCart: CartId={CartId}", cartId);

            var client = _httpClientFactory.CreateClient("OrderService");
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Delete, $"/api/order/cart/{cartId}");

            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
                httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());

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

    // ----------------------------
    // Health
    // ----------------------------

    [HttpGet("health")]
    public IActionResult Health()
        => Ok(new { status = "healthy", service = "MobileBff" });

    /// <summary>Service fee config for cart display. From ServiceFee:Rate, ServiceFee:Minimum, ServiceFee:Cap (env: ServiceFee__*).</summary>
    [HttpGet("config/service-fee")]
    [AllowAnonymous]
    public IActionResult GetServiceFeeConfig()
    {
        var rate = _configuration.GetValue<decimal>("ServiceFee:Rate", 0.05m);
        var minimum = _configuration.GetValue<decimal>("ServiceFee:Minimum", 1.50m);
        var cap = _configuration.GetValue<decimal>("ServiceFee:Cap", 0m);
        return Ok(new { rate, minimum, cap });
    }

    // ----------------------------
    // Vendor Orders
    // ----------------------------

    [HttpGet("vendor/pending-order-counts")]
    [Authorize(Roles = "Vendor,Staff,Admin")]
    public async Task<IActionResult> GetVendorPendingOrderCounts()
    {
        try
        {
            var restClient = _httpClientFactory.CreateClient("RestaurantService");
            ForwardBearerToken(restClient);

            // Fetch both owned and staff-linked restaurant IDs
            var ids = new HashSet<Guid>();
            foreach (var url in new[] { "/api/restaurant/vendor/my-restaurants", "/api/restaurant/staff/my-restaurants" })
            {
                try
                {
                    var resp = await restClient.GetAsync(url);
                    if (!resp.IsSuccessStatusCode) continue;
                    using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        if (TryGetRestaurantId(el, out var id) && id != default) ids.Add(id);
                    }
                }
                catch { /* ignore */ }
            }

            if (ids.Count == 0)
                return Ok(new Dictionary<string, int>());

            var orderClient = _httpClientFactory.CreateClient("OrderService");
            ForwardBearerToken(orderClient);
            var idsParam = string.Join(",", ids);
            var response = await orderClient.GetAsync($"/api/Order/vendor/pending-order-counts?restaurantIds={idsParam}");
            var content = await response.Content.ReadAsStringAsync();
            return new ContentResult { Content = content, ContentType = "application/json", StatusCode = (int)response.StatusCode };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vendor pending order counts");
            return StatusCode(500, new { error = "Failed to fetch pending order counts" });
        }
    }

    [HttpGet("vendor/restaurants/{restaurantId}/orders")]
    [Authorize(Roles = "Vendor,Staff,Admin")]
    public async Task<IActionResult> GetVendorOrders(Guid restaurantId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("OrderService");
            ForwardBearerToken(client);

            var response = await client.GetAsync($"/api/Order/vendor/restaurants/{restaurantId}/orders");
            var content = await response.Content.ReadAsStringAsync();

            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vendor orders");
            return StatusCode(500, new { error = "Failed to fetch vendor orders" });
        }
    }

    [HttpGet("vendor/restaurants/{restaurantId}/orders/{orderId}")]
    [Authorize(Roles = "Vendor,Staff,Admin")]
    public async Task<IActionResult> GetVendorOrder(Guid restaurantId, Guid orderId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("OrderService");
            ForwardBearerToken(client);

            var response = await client.GetAsync($"/api/Order/vendor/restaurants/{restaurantId}/orders/{orderId}");
            var content = await response.Content.ReadAsStringAsync();

            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vendor order {OrderId} for restaurant {RestaurantId}", orderId, restaurantId);
            return StatusCode(500, new { error = "Failed to fetch vendor order" });
        }
    }

    [HttpPut("orders/{orderId}/status")]
    [Authorize(Roles = "Vendor,Staff,Admin")]
    public async Task<IActionResult> UpdateOrderStatus(Guid orderId, [FromBody] UpdateOrderStatusRequest request)
    {
        try
        {
            _logger.LogInformation("UpdateOrderStatus: OrderId={OrderId}, Request={@Request}", orderId, request);

            if (request == null)
                return BadRequest(new { error = "Request body is required" });

            if (string.IsNullOrWhiteSpace(request.Status))
                return BadRequest(new { error = "Status is required" });

            var client = _httpClientFactory.CreateClient("OrderService");
            ForwardBearerToken(client);

            var normalizedRequest = new UpdateOrderStatusRequest(
                request.Status,
                string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes
            );

            var response = await client.PutAsJsonAsync($"/api/Order/{orderId}/status", normalizedRequest);
            var content = await response.Content.ReadAsStringAsync();

            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order status: OrderId={OrderId}", orderId);
            return StatusCode(500, new { error = "Failed to update order status", message = ex.Message });
        }
    }

    // ----------------------------
    // Chat endpoints
    // ----------------------------

    [HttpGet("orders/{orderId}/chat/messages")]
    [Authorize]
    public async Task<IActionResult> GetOrderChatMessages(Guid orderId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("ChatService");
            ForwardBearerToken(client);

            var response = await client.GetAsync($"/api/Chat/orders/{orderId}/messages");
            var content = await response.Content.ReadAsStringAsync();

            return JsonString(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chat messages for order {OrderId}", orderId);
            return StatusCode(500, new { error = "Failed to get chat messages" });
        }
    }

    [HttpGet("orders/{orderId}/chat/unread-count")]
    [Authorize]
    public async Task<IActionResult> GetOrderChatUnreadCount(Guid orderId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("ChatService");
            ForwardBearerToken(client);

            var response = await client.GetAsync($"/api/Chat/orders/{orderId}/unread-count");
            var content = await response.Content.ReadAsStringAsync();

            return JsonString(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread count for order {OrderId}", orderId);
            return StatusCode(500, new { error = "Failed to get unread count" });
        }
    }

    // ----- Generic vendor/customer chat -----

    [HttpPost("vendor-chat/conversations")]
    [Authorize]
    public async Task<IActionResult> CreateOrGetVendorConversation([FromBody] object request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("ChatService");
            ForwardBearerToken(client);
            var json = System.Text.Json.JsonSerializer.Serialize(request);
            var body = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/Chat/vendor/conversations", body);
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating vendor conversation");
            return StatusCode(500, new { error = "Failed to create conversation" });
        }
    }

    [HttpGet("vendor-chat/conversations/mine")]
    [Authorize]
    public async Task<IActionResult> GetMyVendorConversations()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("ChatService");
            ForwardBearerToken(client);
            var response = await client.GetAsync("/api/Chat/vendor/conversations/mine");
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting my vendor conversations");
            return StatusCode(500, new { error = "Failed to get conversations" });
        }
    }

    [HttpGet("vendor-chat/inbox")]
    [Authorize(Roles = "Vendor,Staff,Admin")]
    public async Task<IActionResult> GetVendorInbox([FromQuery] int take = 100)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("ChatService");
            ForwardBearerToken(client);
            var response = await client.GetAsync($"/api/Chat/vendor/inbox?take={take}");
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting vendor inbox");
            return StatusCode(500, new { error = "Failed to get vendor inbox" });
        }
    }

    [HttpGet("vendor-chat/conversations/{conversationId}/messages")]
    [Authorize]
    public async Task<IActionResult> GetVendorConversationMessages(Guid conversationId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("ChatService");
            ForwardBearerToken(client);
            var response = await client.GetAsync($"/api/Chat/vendor/conversations/{conversationId}/messages");
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting vendor conversation messages");
            return StatusCode(500, new { error = "Failed to get messages" });
        }
    }

    [HttpPost("vendor-chat/restaurants/{restaurantId}/broadcast")]
    [Authorize]
    public async Task<IActionResult> BroadcastVendorChat(Guid restaurantId, [FromBody] BroadcastVendorChatRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("ChatService");
            ForwardBearerToken(client);
            var json = System.Text.Json.JsonSerializer.Serialize(request ?? new BroadcastVendorChatRequest(null));
            var body = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"/api/Chat/vendor/restaurants/{restaurantId}/broadcast", body);
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting vendor chat for restaurant {RestaurantId}", restaurantId);
            return StatusCode(500, new { error = "Failed to broadcast message" });
        }
    }

    // ----------------------------
    // Vendor endpoints
    // ----------------------------

    [HttpGet("vendor/my-restaurants")]
    [Authorize(Roles = "Vendor,Staff,Admin")]
    public async Task<IActionResult> GetMyRestaurants()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("RestaurantService");
            ForwardBearerToken(client);

            // Fetch owned restaurants (vendors/admins)
            var response = await client.GetAsync("/api/restaurant/vendor/my-restaurants");
            var ownedJson = response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync() : "[]";

            // Also fetch staff-linked restaurants
            var staffResponse = await client.GetAsync("/api/restaurant/staff/my-restaurants");
            var staffJson = staffResponse.IsSuccessStatusCode ? await staffResponse.Content.ReadAsStringAsync() : "[]";

            // Merge both lists, deduplicating by restaurantId
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var merged = new List<JsonElement>();
            foreach (var json in new[] { ownedJson, staffJson })
            {
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        if (TryGetRestaurantId(el, out var id))
                        {
                            var key = id.ToString();
                            if (seen.Add(key)) merged.Add(el.Clone());
                        }
                    }
                }
                catch { /* ignore parse errors */ }
            }

            return new ContentResult
            {
                Content = System.Text.Json.JsonSerializer.Serialize(merged),
                ContentType = "application/json",
                StatusCode = 200
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vendor restaurants");
            return StatusCode(500, new { error = "Failed to fetch vendor restaurants" });
        }
    }

    // ---- Staff management (Vendor adds/removes staff) ----

    [HttpPost("vendor/restaurants/{restaurantId}/staff")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> AddRestaurantStaff(Guid restaurantId, [FromBody] AddStaffByEmailRequest request)
    {
        try
        {
            // Resolve email to userId via Identity
            var identityClient = _httpClientFactory.CreateClient("IdentityService");
            ForwardBearerToken(identityClient);
            var lookupResp = await identityClient.GetAsync($"/api/auth/user-by-email?email={Uri.EscapeDataString(request.Email)}");
            if (!lookupResp.IsSuccessStatusCode)
                return NotFound(new { error = "No user found with that email" });

            var lookupJson = await lookupResp.Content.ReadAsStringAsync();
            Guid staffUserId;
            try
            {
                using var doc = JsonDocument.Parse(lookupJson);
                var root = doc.RootElement;
                var idStr = (root.TryGetProperty("userId", out var uel) ? uel : root.GetProperty("UserId")).GetString();
                staffUserId = Guid.Parse(idStr!);
            }
            catch
            {
                return NotFound(new { error = "Could not resolve user" });
            }

            // Assign the Staff role to this user if they don't have it
            try
            {
                await identityClient.PostAsJsonAsync("/api/auth/assign-staff-role", new { userId = staffUserId });
            }
            catch { /* best-effort */ }

            // Add staff link in RestaurantService
            var restClient = _httpClientFactory.CreateClient("RestaurantService");
            ForwardBearerToken(restClient);
            var resp = await restClient.PostAsJsonAsync($"/api/restaurant/vendor/{restaurantId}/staff", new { userId = staffUserId });
            var content = await resp.Content.ReadAsStringAsync();
            return StatusCode((int)resp.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding restaurant staff");
            return StatusCode(500, new { error = "Failed to add staff" });
        }
    }

    [HttpDelete("vendor/restaurants/{restaurantId}/staff/{staffUserId}")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> RemoveRestaurantStaff(Guid restaurantId, Guid staffUserId)
    {
        try
        {
            var restClient = _httpClientFactory.CreateClient("RestaurantService");
            ForwardBearerToken(restClient);
            var resp = await restClient.DeleteAsync($"/api/restaurant/vendor/{restaurantId}/staff/{staffUserId}");
            if (!resp.IsSuccessStatusCode)
            {
                var content = await resp.Content.ReadAsStringAsync();
                return StatusCode((int)resp.StatusCode, content);
            }

            // If this user has no remaining staff links, revoke the Staff role
            try
            {
                var countResp = await restClient.GetAsync($"/api/restaurant/staff/{staffUserId}/link-count");
                var remaining = 1; // default to non-zero so we don't accidentally revoke
                if (countResp.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await countResp.Content.ReadAsStringAsync());
                    if (doc.RootElement.TryGetProperty("count", out var countEl))
                        remaining = countEl.GetInt32();
                }

                if (remaining == 0)
                {
                    var identityClient = _httpClientFactory.CreateClient("IdentityService");
                    ForwardBearerToken(identityClient);
                    try
                    {
                        await identityClient.PostAsJsonAsync("/api/auth/revoke-staff-role", new { userId = staffUserId });
                    }
                    catch { /* best-effort role cleanup */ }
                }
            }
            catch { /* best-effort — staff link already removed, which is the important part */ }

            return Ok(new { message = "Staff removed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing restaurant staff");
            return StatusCode(500, new { error = "Failed to remove staff" });
        }
    }

    [HttpGet("vendor/restaurants/{restaurantId}/staff")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> GetRestaurantStaff(Guid restaurantId)
    {
        try
        {
            var restClient = _httpClientFactory.CreateClient("RestaurantService");
            ForwardBearerToken(restClient);
            var resp = await restClient.GetAsync($"/api/restaurant/vendor/{restaurantId}/staff");
            if (!resp.IsSuccessStatusCode)
            {
                var errContent = await resp.Content.ReadAsStringAsync();
                return StatusCode((int)resp.StatusCode, errContent);
            }

            var staffJson = await resp.Content.ReadAsStringAsync();
            using var staffDoc = JsonDocument.Parse(staffJson);
            var staffList = staffDoc.RootElement.EnumerateArray().ToList();

            if (staffList.Count == 0)
                return Ok(Array.Empty<object>());

            // Enrich with email from IdentityService
            var userIds = staffList
                .Select(el =>
                {
                    if (el.TryGetProperty("userId", out var u) || el.TryGetProperty("UserId", out u))
                    {
                        if (Guid.TryParse(u.GetString(), out var id)) return id;
                    }
                    return (Guid?)null;
                })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            var emailMap = new Dictionary<Guid, string>();
            if (userIds.Count > 0)
            {
                try
                {
                    var identityClient = _httpClientFactory.CreateClient("IdentityService");
                    ForwardBearerToken(identityClient);
                    var lookupResp = await identityClient.PostAsJsonAsync("/api/auth/users-by-ids", new { userIds });
                    if (lookupResp.IsSuccessStatusCode)
                    {
                        using var usersDoc = JsonDocument.Parse(await lookupResp.Content.ReadAsStringAsync());
                        foreach (var u in usersDoc.RootElement.EnumerateArray())
                        {
                            var uid = u.TryGetProperty("userId", out var uidEl) ? uidEl.GetString() : null;
                            var email = u.TryGetProperty("email", out var eEl) ? eEl.GetString() : null;
                            if (uid != null && Guid.TryParse(uid, out var parsedId) && !string.IsNullOrWhiteSpace(email))
                                emailMap[parsedId] = email!;
                        }
                    }
                }
                catch { /* best-effort enrichment */ }
            }

            var enriched = staffList.Select(el =>
            {
                var userId = "";
                if (el.TryGetProperty("userId", out var u) || el.TryGetProperty("UserId", out u))
                    userId = u.GetString() ?? "";

                var createdAt = "";
                if (el.TryGetProperty("createdAt", out var c) || el.TryGetProperty("CreatedAt", out c))
                    createdAt = c.GetString() ?? "";

                var restaurantIdStr = "";
                if (el.TryGetProperty("restaurantId", out var r) || el.TryGetProperty("RestaurantId", out r))
                    restaurantIdStr = r.GetString() ?? "";

                string? email = null;
                if (Guid.TryParse(userId, out var parsedUid))
                    emailMap.TryGetValue(parsedUid, out email);

                return new { userId, restaurantId = restaurantIdStr, createdAt, email };
            }).ToList();

            return Ok(enriched);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching restaurant staff");
            return StatusCode(500, new { error = "Failed to fetch staff" });
        }
    }

    [HttpPost("vendor/restaurants")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> CreateRestaurant([FromBody] object request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("RestaurantService");
            ForwardBearerToken(client);

            var json = System.Text.Json.JsonSerializer.Serialize(request);
            var body = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/restaurant", body);

            var content = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating restaurant");
            return StatusCode(500, new { error = "Failed to create restaurant" });
        }
    }

    [HttpPut("vendor/restaurants/{restaurantId}")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> UpdateRestaurant(Guid restaurantId, [FromBody] object request)
    {
        try
        {
            var restClient = _httpClientFactory.CreateClient("RestaurantService");
            ForwardBearerToken(restClient);

            string? oldImageUrl = null;
            var getResp = await restClient.GetAsync($"/api/restaurant/{restaurantId}");
            if (getResp.IsSuccessStatusCode)
            {
                var current = JsonSerializer.Deserialize<JsonElement>(await getResp.Content.ReadAsStringAsync());
                if (current.TryGetProperty("imageUrl", out var imgProp) || current.TryGetProperty("ImageUrl", out imgProp))
                    oldImageUrl = imgProp.GetString();
            }

            var json = JsonSerializer.Serialize(request);
            string? newImageUrl = null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("imageUrl", out var newImgProp) || doc.RootElement.TryGetProperty("ImageUrl", out newImgProp))
                    newImageUrl = newImgProp.GetString();
            }
            catch { /* ignore */ }

            var body = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await restClient.PutAsync($"/api/restaurant/{restaurantId}", body);

            if (response.IsSuccessStatusCode)
                await TryDeleteOldAppImageAsync(oldImageUrl, newImageUrl);

            var content = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating restaurant");
            return StatusCode(500, new { error = "Failed to update restaurant" });
        }
    }

    [HttpDelete("vendor/restaurants/{restaurantId}")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> DeleteRestaurant(Guid restaurantId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("RestaurantService");
            ForwardBearerToken(client);

            var response = await client.DeleteAsync($"/api/restaurant/vendor/{restaurantId}");
            var content = await response.Content.ReadAsStringAsync();

            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting restaurant");
            return StatusCode(500, new { error = "Failed to delete restaurant" });
        }
    }

    // Menu item management endpoints
    [HttpPost("restaurants/{restaurantId}/menu-items")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> CreateMenuItem(Guid restaurantId, [FromBody] object request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("CatalogService");
            ForwardBearerToken(client);

            var json = System.Text.Json.JsonSerializer.Serialize(request);
            var body = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"/api/catalog/restaurants/{restaurantId}/menu-items", body);

            var content = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating menu item");
            return StatusCode(500, new { error = "Failed to create menu item" });
        }
    }

    [HttpPut("menu-items/{menuItemId}")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> UpdateMenuItem(Guid menuItemId, [FromBody] object request)
    {
        try
        {
            var catClient = _httpClientFactory.CreateClient("CatalogService");
            ForwardBearerToken(catClient);

            string? oldImageUrl = null;
            var getResp = await catClient.GetAsync($"/api/catalog/menu-items/{menuItemId}");
            if (getResp.IsSuccessStatusCode)
            {
                var current = JsonSerializer.Deserialize<JsonElement>(await getResp.Content.ReadAsStringAsync());
                if (current.TryGetProperty("imageUrl", out var imgProp) || current.TryGetProperty("ImageUrl", out imgProp))
                    oldImageUrl = imgProp.GetString();
            }

            var json = JsonSerializer.Serialize(request);
            string? newImageUrl = null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("imageUrl", out var newImgProp) || doc.RootElement.TryGetProperty("ImageUrl", out newImgProp))
                    newImageUrl = newImgProp.GetString();
            }
            catch { /* ignore */ }

            var body = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await catClient.PutAsync($"/api/catalog/menu-items/{menuItemId}", body);

            if (response.IsSuccessStatusCode)
                await TryDeleteOldAppImageAsync(oldImageUrl, newImageUrl);

            var content = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating menu item");
            return StatusCode(500, new { error = "Failed to update menu item" });
        }
    }

    [HttpPatch("menu-items/{menuItemId}/availability")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> ToggleMenuItemAvailability(Guid menuItemId, [FromBody] object request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("CatalogService");
            ForwardBearerToken(client);

            var json = System.Text.Json.JsonSerializer.Serialize(request);
            var body = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PatchAsync($"/api/catalog/menu-items/{menuItemId}/availability", body);

            var content = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling menu item availability");
            return StatusCode(500, new { error = "Failed to toggle menu item availability" });
        }
    }

    // ----------------------------
    // Admin endpoints
    // ----------------------------

    [HttpGet("admin/restaurants")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllRestaurants([FromQuery] int skip = 0, [FromQuery] int take = 100)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("RestaurantService");
            ForwardBearerToken(client);

            var response = await client.GetAsync($"/api/restaurant/admin/all?skip={skip}&take={take}");
            var content = await response.Content.ReadAsStringAsync();

            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching all restaurants (admin)");
            return StatusCode(500, new { error = "Failed to fetch restaurants" });
        }
    }

    [HttpPatch("admin/restaurants/{restaurantId}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminToggleRestaurantStatus(Guid restaurantId, [FromBody] object request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("RestaurantService");
            ForwardBearerToken(client);

            var json = System.Text.Json.JsonSerializer.Serialize(request);
            var body = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PatchAsync($"/api/restaurant/admin/{restaurantId}/status", body);

            var content = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling restaurant status (admin)");
            return StatusCode(500, new { error = "Failed to toggle restaurant status" });
        }
    }

    [HttpDelete("admin/restaurants/{restaurantId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminDeleteRestaurant(Guid restaurantId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("RestaurantService");
            ForwardBearerToken(client);

            var response = await client.DeleteAsync($"/api/restaurant/admin/{restaurantId}");
            var content = await response.Content.ReadAsStringAsync();

            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting restaurant (admin)");
            return StatusCode(500, new { error = "Failed to delete restaurant" });
        }
    }

    // ----------------------------
    // Reviews
    // ----------------------------

    [HttpPost("reviews")]
    [Authorize]
    public async Task<IActionResult> CreateReview([FromBody] CreateReviewRequest request)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(new { message = "Request body is required" });
            }

            var client = _httpClientFactory.CreateClient("ReviewService");
            ForwardBearerToken(client);

            var response = await client.PostAsJsonAsync("/api/review", request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ReviewService returned {StatusCode} when creating review: {Content}",
                    response.StatusCode, content);
            }

            return JsonString(content, (int)response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "ReviewService unreachable when creating review. Ensure ReviewService is running (port 5009).");
            return StatusCode(503, new { message = "Review service is temporarily unavailable. Please try again later." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating review. Error: {Message}", ex.Message);
            return StatusCode(500, new { message = "Failed to create review. Please try again later." });
        }
    }

    [HttpGet("reviews/{reviewId}")]
    public async Task<IActionResult> GetReview(Guid reviewId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("ReviewService");
            var response = await client.GetAsync($"/api/review/{reviewId}");
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting review");
            return StatusCode(500, new { message = "Failed to get review" });
        }
    }

    [HttpGet("reviews/restaurant/{restaurantId}")]
    public async Task<IActionResult> GetReviewsByRestaurant(
        Guid restaurantId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("ReviewService");
            var response = await client.GetAsync($"/api/review/restaurant/{restaurantId}?skip={skip}&take={take}");
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ReviewService returned {StatusCode} for restaurant {RestaurantId}: {Content}",
                    response.StatusCode, restaurantId, content);
            }

            return JsonString(content, (int)response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "ReviewService unreachable when getting reviews for restaurant {RestaurantId}. Ensure ReviewService is running (port 5009).", restaurantId);
            return StatusCode(503, new { message = "Review service is temporarily unavailable. Please try again later." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting restaurant reviews for {RestaurantId}", restaurantId);
            return StatusCode(500, new { message = "Failed to get reviews" });
        }
    }

    [HttpGet("reviews/restaurant/{restaurantId}/rating")]
    public async Task<IActionResult> GetRestaurantRating(Guid restaurantId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("ReviewService");
            var response = await client.GetAsync($"/api/review/restaurant/{restaurantId}/rating");
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ReviewService returned {StatusCode} for rating of restaurant {RestaurantId}: {Content}",
                    response.StatusCode, restaurantId, content);
            }

            return JsonString(content, (int)response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "ReviewService unreachable when getting rating for restaurant {RestaurantId}. Ensure ReviewService is running (port 5009).", restaurantId);
            // Return default rating instead of error
            return JsonString(System.Text.Json.JsonSerializer.Serialize(new
            {
                restaurantId = restaurantId,
                averageRating = 0m,
                totalReviews = 0,
                ratingDistribution = new Dictionary<int, int>()
            }), 200);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting restaurant rating for {RestaurantId}", restaurantId);
            // Return default rating instead of error
            return JsonString(System.Text.Json.JsonSerializer.Serialize(new
            {
                restaurantId = restaurantId,
                averageRating = 0m,
                totalReviews = 0,
                ratingDistribution = new Dictionary<int, int>()
            }), 200);
        }
    }

    [HttpGet("reviews/me")]
    [Authorize]
    public async Task<IActionResult> GetMyReviews(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("ReviewService");
            ForwardBearerToken(client);
            var response = await client.GetAsync($"/api/review/me?skip={skip}&take={take}");
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ReviewService returned {StatusCode} for user reviews: {Content}",
                    response.StatusCode, content);
            }

            return JsonString(content, (int)response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "ReviewService unreachable when getting user reviews. Ensure ReviewService is running (port 5009).");
            // Return empty list instead of error to prevent UI crashes
            return JsonString("[]", 200);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user reviews");
            // Return empty list instead of error to prevent UI crashes
            return JsonString("[]", 200);
        }
    }

    [HttpPut("reviews/{reviewId}")]
    [Authorize]
    public async Task<IActionResult> UpdateReview(Guid reviewId, [FromBody] UpdateReviewDto dto)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("ReviewService");
            ForwardBearerToken(client);
            var response = await client.PutAsJsonAsync($"/api/review/{reviewId}", dto);
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating review");
            return StatusCode(500, new { message = "Failed to update review" });
        }
    }

    [HttpPost("reviews/{reviewId}/response")]
    [Authorize(Roles = "RestaurantOwner")]
    public async Task<IActionResult> AddRestaurantResponse(Guid reviewId, [FromBody] AddResponseRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("ReviewService");
            ForwardBearerToken(client);
            var response = await client.PostAsJsonAsync($"/api/review/{reviewId}/response", request);
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding restaurant response");
            return StatusCode(500, new { message = "Failed to add response" });
        }
    }

    // ----------------------------
    // Document endpoints
    // ----------------------------

    [HttpPost("documents/upload-restaurant-image")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> UploadRestaurantImage([FromForm] IFormFile file, [FromForm] string? replacePath = null)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "File is required" });

            var client = _httpClientFactory.CreateClient("DocumentService");
            ForwardBearerToken(client);

            MemoryStream memoryStream;
            using (var fileStream = file.OpenReadStream())
            {
                memoryStream = new MemoryStream();
                await fileStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
            }

            try
            {
                using var content = new MultipartFormDataContent();
                content.Add(new StreamContent(memoryStream), "file", file.FileName);
                if (!string.IsNullOrWhiteSpace(replacePath))
                    content.Add(new StringContent(replacePath), "replacePath");

                var response = await client.PostAsync("/api/document/upload-restaurant-image", content);
                var responseContent = await response.Content.ReadAsStringAsync();
                return JsonString(responseContent, (int)response.StatusCode);
            }
            finally
            {
                memoryStream?.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading restaurant image");
            return StatusCode(500, new { message = "Failed to upload image" });
        }
    }

    [HttpPost("documents/upload-menu-image")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> UploadMenuImage([FromForm] IFormFile file, [FromForm] string? replacePath = null)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "File is required" });

            var client = _httpClientFactory.CreateClient("DocumentService");
            ForwardBearerToken(client);

            MemoryStream memoryStream;
            using (var fileStream = file.OpenReadStream())
            {
                memoryStream = new MemoryStream();
                await fileStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
            }

            try
            {
                using var content = new MultipartFormDataContent();
                content.Add(new StreamContent(memoryStream), "file", file.FileName);
                if (!string.IsNullOrWhiteSpace(replacePath))
                    content.Add(new StringContent(replacePath), "replacePath");

                var response = await client.PostAsync("/api/document/upload-menu-image", content);
                var responseContent = await response.Content.ReadAsStringAsync();
                return JsonString(responseContent, (int)response.StatusCode);
            }
            finally
            {
                memoryStream?.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading menu image");
            return StatusCode(500, new { message = "Failed to upload image" });
        }
    }

    [HttpGet("menu-image")]
    [AllowAnonymous]
    public async Task<IActionResult> GetMenuImage([FromQuery] string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return BadRequest();

        try
        {
            var client = _httpClientFactory.CreateClient("DocumentServiceNoRedirect");
            var encodedPath = Uri.EscapeDataString(path);
            var response = await client.GetAsync($"/api/document/menu-image-url?path={encodedPath}");
            if (response.StatusCode == System.Net.HttpStatusCode.Redirect
                || response.StatusCode == System.Net.HttpStatusCode.MovedPermanently
                || (int)response.StatusCode >= 300 && (int)response.StatusCode < 400)
            {
                var location = response.Headers.Location;
                if (location != null)
                    return Redirect(location.ToString());
            }
            return StatusCode((int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get menu image for path: {Path}", path);
            return NotFound();
        }
    }

    [HttpPost("documents/upload")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> UploadDocument(
        [FromForm] IFormFile file,
        [FromForm] string documentType,
        [FromForm] string? notes = null,
        [FromForm] DateTime? expiresAt = null)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("Upload request received with null or empty file");
                return BadRequest(new { message = "File is required" });
            }

            if (string.IsNullOrEmpty(documentType))
            {
                _logger.LogWarning("Upload request received without documentType");
                return BadRequest(new { message = "Document type is required" });
            }

            _logger.LogInformation("Uploading document: {FileName}, Type: {DocumentType}, Size: {FileSize}",
                file.FileName, documentType, file.Length);

            var client = _httpClientFactory.CreateClient("DocumentService");
            ForwardBearerToken(client);

            // Copy file stream to memory stream to avoid disposal issues
            MemoryStream memoryStream;
            using (var fileStream = file.OpenReadStream())
            {
                memoryStream = new MemoryStream();
                await fileStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
            }

            try
            {
                using var content = new MultipartFormDataContent();
                content.Add(new StreamContent(memoryStream), "file", file.FileName);
                content.Add(new StringContent(documentType), "documentType");
                if (!string.IsNullOrEmpty(notes))
                    content.Add(new StringContent(notes), "notes");
                if (expiresAt.HasValue)
                    content.Add(new StringContent(expiresAt.Value.ToString("O")), "expiresAt");

                var response = await client.PostAsync("/api/document/upload", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("DocumentService returned error: {StatusCode} - {Content}",
                        response.StatusCode, responseContent);
                }

                return JsonString(responseContent, (int)response.StatusCode);
            }
            finally
            {
                memoryStream?.Dispose();
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error uploading document: {Message}", ex.Message);
            return StatusCode(500, new { message = $"Failed to connect to DocumentService: {ex.Message}" });
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout uploading document");
            return StatusCode(500, new { message = "Upload timeout - DocumentService may be unavailable" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document: {Message}", ex.Message);
            return StatusCode(500, new { message = $"Failed to upload document: {ex.Message}" });
        }
    }

    [HttpGet("documents/vendor/my-documents")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> GetMyDocuments([FromQuery] bool? isActive = null)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DocumentService");
            ForwardBearerToken(client);

            var queryString = isActive.HasValue ? $"?isActive={isActive.Value}" : "";
            var response = await client.GetAsync($"/api/document/vendor/my-documents{queryString}");
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vendor documents");
            return StatusCode(500, new { message = "Failed to fetch documents" });
        }
    }

    [HttpGet("documents/{documentId:guid}")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> GetDocument(Guid documentId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DocumentService");
            ForwardBearerToken(client);

            var response = await client.GetAsync($"/api/document/{documentId}");
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching document");
            return StatusCode(500, new { message = "Failed to fetch document" });
        }
    }

    [HttpPatch("documents/{documentId:guid}/status")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> UpdateDocumentStatus(Guid documentId, [FromBody] UpdateDocumentStatusRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DocumentService");
            ForwardBearerToken(client);

            var response = await client.PatchAsJsonAsync($"/api/document/{documentId}/status", request);
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating document status");
            return StatusCode(500, new { message = "Failed to update document status" });
        }
    }

    [HttpDelete("documents/{documentId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteDocument(Guid documentId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DocumentService");
            ForwardBearerToken(client);

            var response = await client.DeleteAsync($"/api/document/{documentId}");
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document");
            return StatusCode(500, new { message = "Failed to delete document" });
        }
    }

    // Admin document endpoints
    [HttpGet("documents/admin/all")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllDocuments(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 500,
        [FromQuery] Guid? vendorId = null,
        [FromQuery] bool? isActive = null)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DocumentService");
            ForwardBearerToken(client);

            var queryParams = new List<string> { "skip=" + skip, "take=" + take };
            if (vendorId.HasValue) queryParams.Add($"vendorId={vendorId.Value}");
            if (isActive.HasValue) queryParams.Add($"isActive={isActive.Value}");

            var queryString = "?" + string.Join("&", queryParams);
            var response = await client.GetAsync($"/api/document/admin/all{queryString}");
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                return JsonString(content, (int)response.StatusCode);

            // Enrich with vendor first + last name from CustomerService
            var docs = JsonSerializer.Deserialize<List<JsonElement>>(content);
            if (docs == null || docs.Count == 0)
                return JsonString(content);

            var vendorIds = docs
                .Select(d =>
                {
                    if (!d.TryGetProperty("vendorId", out var v)) return (Guid?)null;
                    var s = v.GetString();
                    return string.IsNullOrEmpty(s) || !Guid.TryParse(s, out var g) ? (Guid?)null : g;
                })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var customerClient = _httpClientFactory.CreateClient("CustomerService");
            var vendorNames = new Dictionary<Guid, string>();
            foreach (var vid in vendorIds)
            {
                try
                {
                    var custResponse = await customerClient.GetAsync($"api/Customer/by-user/{vid}");
                    if (custResponse.IsSuccessStatusCode)
                    {
                        var cust = await custResponse.Content.ReadFromJsonAsync<CustomerInfoDto>(jsonOptions);
                        if (cust != null)
                            vendorNames[vid] = $"{cust.FirstName} {cust.LastName}".Trim();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not resolve vendor name for {VendorId}", vid);
                }
            }

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
            {
                writer.WriteStartArray();
                foreach (var doc in docs)
                {
                    writer.WriteStartObject();
                    foreach (var prop in doc.EnumerateObject())
                    {
                        prop.WriteTo(writer);
                    }
                    if (doc.TryGetProperty("vendorId", out var vProp) && Guid.TryParse(vProp.GetString(), out var vId) && vendorNames.TryGetValue(vId, out var name))
                    {
                        writer.WriteString("vendorName", name);
                    }
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
            var enriched = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            return JsonString(enriched);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching all documents");
            return StatusCode(500, new { message = "Failed to fetch documents" });
        }
    }

    [HttpPatch("documents/admin/{documentId:guid}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminUpdateDocumentStatus(Guid documentId, [FromBody] UpdateDocumentStatusRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DocumentService");
            ForwardBearerToken(client);

            var response = await client.PatchAsJsonAsync($"/api/document/admin/{documentId}/status", request);
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating document status");
            return StatusCode(500, new { message = "Failed to update document status" });
        }
    }

    [HttpDelete("documents/admin/{documentId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminDeleteDocument(Guid documentId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DocumentService");
            ForwardBearerToken(client);

            var response = await client.DeleteAsync($"/api/document/admin/{documentId}");
            var content = await response.Content.ReadAsStringAsync();
            return JsonString(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document");
            return StatusCode(500, new { message = "Failed to delete document" });
        }
    }
}

// ----------------------------
// DTOs
// ----------------------------

public record CreateCartRequest(Guid? RestaurantId);

public record AddCartItemRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("menuItemId")]
    public Guid MenuItemId { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("isCustomRequest")]
    public bool IsCustomRequest { get; init; }

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
    string? SpecialInstructions,
    string? IdempotencyKey,
    string? SuccessUrl,
    string? CancelUrl
);

public record RetryPaymentRequest(string? SuccessUrl, string? CancelUrl);

public record RegisterRequest(string FirstName, string LastName, string? DisplayName, string Email, string PhoneNumber, string Password, string? Role);
public record LoginRequest(string Email, string Password);
public record GoogleLoginRequest(string IdToken);
public record AppleLoginRequest(string IdToken, string? Email, string? FullName);
public record RefreshTokenRequest(string RefreshToken);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Token, string Email, string NewPassword, string ConfirmPassword);
public record UpdateOrderStatusRequest(string Status, string? Notes);
public record BroadcastVendorChatRequest(string? Message);
public record CreateReviewRequest(Guid OrderId, Guid RestaurantId, CreateReviewDto Review);
public record RegisterPushTokenRequest(string PushToken, string? DeviceId, string? Platform, string? DeviceName);
public record UnregisterPushTokenRequest(string? PushToken, string? DeviceId);
public record CreateReviewDto(int Rating, string? Comment, List<string>? Tags);
public record UpdateReviewDto(int? Rating, string? Comment, List<string>? Tags);
public record AddResponseRequest(string Response);
public record UpdateDocumentStatusRequest(bool IsActive);
public record AssignRoleRequest(string Email, string Role);
public record RevokeRoleRequest(string Email, string Role);
public record CustomerInfoDto(Guid CustomerId, Guid UserId, string FirstName, string LastName, string? Email, string? PhoneNumber);
public record AddStaffByEmailRequest(string Email);
