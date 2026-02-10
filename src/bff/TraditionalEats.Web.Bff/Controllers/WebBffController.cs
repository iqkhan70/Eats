using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using TraditionalEats.BuildingBlocks.Redis;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Headers;

namespace TraditionalEats.Web.Bff.Controllers;

[ApiController]
[Route("api/WebBff")]
public class WebBffController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebBffController> _logger;
    private readonly ICartSessionService _cartSessionService;
    private readonly IConfiguration _configuration;
    private const string SESSION_COOKIE_NAME = "cart_session";

    public WebBffController(
        IHttpClientFactory httpClientFactory,
        ILogger<WebBffController> logger,
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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private void ForwardBearerToken(HttpClient client)
    {
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(authHeader))
        {
            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", authHeader["Bearer ".Length..].Trim());
            }
            else
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", authHeader.Trim());
            }
        }
    }

    private ContentResult JsonContent(string json, int statusCode)
        => new ContentResult
        {
            Content = json,
            ContentType = "application/json",
            StatusCode = statusCode
        };

    private async Task<string> GetOrCreateSessionIdAsync()
    {
        // Try header (Blazor WASM)
        string? existingSessionId = null;
        if (Request.Headers.TryGetValue("X-Cart-Session-Id", out var headerSessionId))
        {
            var headerValue = headerSessionId.ToString();
            if (!string.IsNullOrWhiteSpace(headerValue))
            {
                existingSessionId = headerValue;
            }
        }

        // Fallback cookie (server-side)
        if (string.IsNullOrEmpty(existingSessionId)
            && Request.Cookies.TryGetValue(SESSION_COOKIE_NAME, out var cookieSessionId)
            && !string.IsNullOrEmpty(cookieSessionId))
        {
            existingSessionId = cookieSessionId;
        }

        string sessionId;
        try
        {
            sessionId = await _cartSessionService.GetOrCreateSessionIdAsync(existingSessionId);
        }
        catch (Exception ex)
        {
            // Redis unavailable - generate a fallback session ID
            _logger.LogWarning(ex, "Redis unavailable when getting session ID, generating fallback");
            if (!string.IsNullOrEmpty(existingSessionId))
            {
                sessionId = existingSessionId;
            }
            else
            {
                // Generate a new session ID (GUID)
                sessionId = Guid.NewGuid().ToString();
            }
        }

        // If new, set cookie (server-side)
        if (existingSessionId != sessionId && string.IsNullOrEmpty(existingSessionId))
        {
            Response.Cookies.Append(SESSION_COOKIE_NAME, sessionId, new CookieOptions
            {
                HttpOnly = true,
                Secure = false, // true in prod (HTTPS)
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });
        }

        return sessionId;
    }

    // ----------------------------
    // Restaurants
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

    [HttpGet("restaurants")]
    public async Task<IActionResult> GetRestaurants(
        [FromQuery] string? location,
        [FromQuery] string? cuisineType,
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
            if (!string.IsNullOrEmpty(cuisineType)) queryParams.Add($"cuisineType={Uri.EscapeDataString(cuisineType)}");
            if (latitude.HasValue) queryParams.Add($"latitude={latitude.Value}");
            if (longitude.HasValue) queryParams.Add($"longitude={longitude.Value}");
            if (radiusMiles.HasValue) queryParams.Add($"radiusMiles={radiusMiles.Value}");
            if (!string.IsNullOrEmpty(zip)) queryParams.Add($"zip={Uri.EscapeDataString(zip)}");
            queryParams.Add($"skip={skip}");
            queryParams.Add($"take={take}");

            var queryString = queryParams.Any() ? "?" + string.Join("&", queryParams) : "";
            var response = await client.GetAsync($"/api/restaurant{queryString}");
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("RestaurantService returned {StatusCode}: {Content}", response.StatusCode, content);
            return JsonContent(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching restaurants. Ensure RestaurantService is running (e.g. port 5007).");
            return StatusCode(500, new { error = "Failed to fetch restaurants", detail = ex.Message });
        }
    }

    [HttpGet("restaurants/{id}")]
    public async Task<IActionResult> GetRestaurant(Guid id)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("RestaurantService");
            var response = await client.GetAsync($"/api/restaurant/{id}");
            var content = await response.Content.ReadAsStringAsync();

            return JsonContent(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching restaurant {RestaurantId}", id);
            return StatusCode(500, new { error = "Failed to fetch restaurant" });
        }
    }

    // ----------------------------
    // Orders (OPTION A FIX)
    // ----------------------------

    /// <summary>
    /// Grid-friendly orders endpoint:
    /// /api/WebBff/orders?restaurantId=...&skip=0&take=20&orderBy=CreatedAt%20desc&q=...
    /// Returns: { items: [...], totalCount: N }
    /// </summary>
    // Customer orders endpoint (must come before vendor orders to avoid routing conflict)
    [HttpGet("orders")]
    [Authorize]
    public async Task<IActionResult> GetCustomerOrders()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("OrderService");

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/order");
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
            }

            var response = await client.SendAsync(httpRequestMessage);
            var content = await response.Content.ReadAsStringAsync();
            return JsonContent(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customer orders");
            return StatusCode(500, new { error = "Failed to get orders" });
        }
    }

    [HttpGet("vendor/orders")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> GetVendorOrders(
        [FromQuery] Guid? restaurantId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        [FromQuery] string? orderBy = null,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null
    )
    {
        try
        {
            if (take <= 0) take = 20;
            if (take > 200) take = 200;
            if (skip < 0) skip = 0;

            var isAdmin = User.IsInRole("Admin");

            // Which restaurants can this user access?
            var allowedRestaurantIds = await GetAllowedRestaurantIdsAsync(isAdmin);

            _logger.LogInformation("GetVendorOrders: User isAdmin={IsAdmin}, AllowedRestaurantIds={RestaurantIds}",
                isAdmin, string.Join(", ", allowedRestaurantIds));

            if (allowedRestaurantIds.Count == 0)
            {
                _logger.LogWarning("GetVendorOrders: No allowed restaurants found for user. User may not have any restaurants assigned.");
                var empty = new PagedResult<OrderDto>(Array.Empty<OrderDto>(), 0);
                return JsonContent(JsonSerializer.Serialize(empty, JsonOptions), 200);
            }

            // If restaurantId is provided, enforce it is allowed
            if (restaurantId.HasValue && restaurantId.Value != Guid.Empty)
            {
                if (!allowedRestaurantIds.Contains(restaurantId.Value))
                {
                    return Forbid();
                }
            }

            // Build OData query string for server-side pagination, filtering, and sorting
            var queryParams = new List<string>();
            var filterParts = new List<string>();

            // Filter by restaurantId if provided
            if (restaurantId.HasValue && restaurantId.Value != Guid.Empty)
            {
                // OData GUID format: use the GUID string directly (more compatible)
                filterParts.Add($"RestaurantId eq {restaurantId.Value}");
            }
            else if (!isAdmin)
            {
                // For vendors, filter by allowed restaurants
                if (allowedRestaurantIds.Count > 0)
                {
                    var restaurantFilter = string.Join(" or ", allowedRestaurantIds.Select(id => $"RestaurantId eq {id}"));
                    filterParts.Add($"({restaurantFilter})");
                }
                else
                {
                    // No allowed restaurants - return empty result
                    filterParts.Add("1 eq 0"); // Always false filter
                }
            }

            // Add status filter if provided
            if (!string.IsNullOrWhiteSpace(status))
            {
                var statusValue = status.Trim().Replace("'", "''"); // Escape single quotes for OData
                filterParts.Add($"Status eq '{statusValue}'");
            }

            // Add search query if provided (search in order ID or delivery address)
            if (!string.IsNullOrWhiteSpace(q))
            {
                var searchTerm = q.Trim().Replace("'", "''"); // Escape single quotes for OData
                // OData search: contains on OrderId (first 8 chars) or DeliveryAddress
                filterParts.Add($"(contains(tostring(OrderId), '{searchTerm}') or contains(DeliveryAddress, '{searchTerm}'))");
            }

            // Combine all filter parts with 'and'
            if (filterParts.Count > 0)
            {
                var combinedFilter = string.Join(" and ", filterParts);
                queryParams.Add($"$filter={Uri.EscapeDataString(combinedFilter)}");
            }

            // Add ordering - map DTO property names to entity property names
            if (!string.IsNullOrWhiteSpace(orderBy))
            {
                // Convert "CreatedAt desc" to OData format
                var orderParts = orderBy.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var field = orderParts[0];
                var direction = orderParts.Length > 1 && orderParts[1].Equals("desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";

                // Map DTO property names to entity property names
                // OrderShort -> OrderId (OrderShort is a computed property in DTO)
                // RestaurantName -> RestaurantId (RestaurantName is not on Order entity, use RestaurantId)
                // Status, CreatedAt, Total are the same on both
                var entityField = field switch
                {
                    "OrderShort" => "OrderId",
                    "RestaurantName" => "RestaurantId", // Can't sort by name directly, use ID
                    _ => field // Keep as-is for Status, CreatedAt, Total, etc.
                };

                queryParams.Add($"$orderby={Uri.EscapeDataString($"{entityField} {direction}")}");
            }
            else
            {
                queryParams.Add($"$orderby={Uri.EscapeDataString("CreatedAt desc")}");
            }

            // Add pagination
            queryParams.Add($"$skip={skip}");
            queryParams.Add($"$top={take}");
            queryParams.Add("$count=true"); // Include total count

            // Note: $expand removed - navigation properties are included via .Include() in the controller
            // and will be serialized automatically by OData

            // Build OData URL
            var odataUrl = $"/odata/Orders?{string.Join("&", queryParams)}";

            _logger.LogInformation("OData URL: {ODataUrl}", odataUrl);

            // Call OrderService OData endpoint
            var client = _httpClientFactory.CreateClient("OrderService");
            ForwardBearerToken(client);

            var response = await client.GetAsync(odataUrl);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OData request failed: {StatusCode} - URL: {Url} - Content: {Content}", response.StatusCode, odataUrl, content);

                // Try to extract error message from OData response
                string errorMessage = "Failed to fetch orders from OData endpoint";
                try
                {
                    var errorResponse = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);
                    if (errorResponse.TryGetProperty("error", out var errorProp))
                    {
                        if (errorProp.ValueKind == JsonValueKind.Object && errorProp.TryGetProperty("message", out var messageProp))
                        {
                            errorMessage = messageProp.GetString() ?? errorMessage;
                        }
                        else if (errorProp.ValueKind == JsonValueKind.String)
                        {
                            errorMessage = errorProp.GetString() ?? errorMessage;
                        }
                    }
                    else if (errorResponse.TryGetProperty("message", out var msgProp))
                    {
                        errorMessage = msgProp.GetString() ?? errorMessage;
                    }
                }
                catch
                {
                    // If parsing fails, use default message
                }

                return StatusCode((int)response.StatusCode, new { error = errorMessage, details = content });
            }

            // OData returns the data in a specific format
            // We need to parse it and return in our expected format
            try
            {
                var odataResponse = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);

                // OData response structure: { "@odata.context": "...", "value": [...], "@odata.count": N }
                var orders = new List<OrderDto>();
                var totalCount = 0;

                if (odataResponse.TryGetProperty("value", out var valueArray))
                {
                    // Try to deserialize directly first (simpler and faster)
                    try
                    {
                        orders = JsonSerializer.Deserialize<List<OrderDto>>(valueArray.GetRawText(), JsonOptions) ?? new();
                        _logger.LogInformation("Successfully deserialized orders directly from OData value array");
                    }
                    catch (Exception directDeserializeEx)
                    {
                        _logger.LogWarning(directDeserializeEx, "Direct deserialization failed, falling back to manual parsing");

                        // Fallback: Parse each order individually to ensure navigation properties are included
                        // OData may return properties in PascalCase or camelCase depending on serialization settings
                        foreach (var orderElement in valueArray.EnumerateArray())
                        {
                            try
                            {
                                // Helper to get property value (tries both camelCase and PascalCase)
                                string? GetString(JsonElement element, params string[] propertyNames)
                                {
                                    foreach (var propName in propertyNames)
                                    {
                                        if (element.TryGetProperty(propName, out var prop))
                                            return prop.GetString();
                                    }
                                    return null;
                                }

                                Guid GetGuid(JsonElement element, params string[] propertyNames)
                                {
                                    var str = GetString(element, propertyNames);
                                    return Guid.TryParse(str, out var guid) ? guid : Guid.Empty;
                                }

                                DateTime GetDateTime(JsonElement element, params string[] propertyNames)
                                {
                                    foreach (var propName in propertyNames)
                                    {
                                        if (element.TryGetProperty(propName, out var prop))
                                            return prop.GetDateTime();
                                    }
                                    return DateTime.UtcNow;
                                }

                                decimal GetDecimal(JsonElement element, params string[] propertyNames)
                                {
                                    foreach (var propName in propertyNames)
                                    {
                                        if (element.TryGetProperty(propName, out var prop))
                                            return prop.GetDecimal();
                                    }
                                    return 0m;
                                }

                                int GetInt32(JsonElement element, params string[] propertyNames)
                                {
                                    foreach (var propName in propertyNames)
                                    {
                                        if (element.TryGetProperty(propName, out var prop))
                                            return prop.GetInt32();
                                    }
                                    return 0;
                                }

                                var order = new OrderDto
                                {
                                    OrderId = GetGuid(orderElement, "orderId", "OrderId"),
                                    CustomerId = GetGuid(orderElement, "customerId", "CustomerId"),
                                    RestaurantId = GetGuid(orderElement, "restaurantId", "RestaurantId"),
                                    Status = GetString(orderElement, "status", "Status") ?? "Pending",
                                    CreatedAt = GetDateTime(orderElement, "createdAt", "CreatedAt"),
                                    DeliveryAddress = GetString(orderElement, "deliveryAddress", "DeliveryAddress"),
                                    SpecialInstructions = GetString(orderElement, "specialInstructions", "SpecialInstructions"),
                                    Subtotal = GetDecimal(orderElement, "subtotal", "Subtotal"),
                                    Tax = GetDecimal(orderElement, "tax", "Tax"),
                                    DeliveryFee = GetDecimal(orderElement, "deliveryFee", "DeliveryFee"),
                                    ServiceFee = GetDecimal(orderElement, "serviceFee", "ServiceFee"),
                                    Total = GetDecimal(orderElement, "total", "Total"),
                                    Items = new List<OrderItemDto>(),
                                    StatusHistory = new List<OrderStatusHistoryDto>()
                                };

                                // Parse Items (expanded navigation property) - try both camelCase and PascalCase
                                JsonElement itemsArray;
                                if (orderElement.TryGetProperty("items", out itemsArray) || orderElement.TryGetProperty("Items", out itemsArray))
                                {
                                    foreach (var itemElement in itemsArray.EnumerateArray())
                                    {
                                        order.Items.Add(new OrderItemDto
                                        {
                                            OrderItemId = GetGuid(itemElement, "orderItemId", "OrderItemId"),
                                            Name = GetString(itemElement, "name", "Name") ?? "",
                                            Quantity = GetInt32(itemElement, "quantity", "Quantity"),
                                            TotalPrice = GetDecimal(itemElement, "totalPrice", "TotalPrice")
                                        });
                                    }
                                }

                                // Parse StatusHistory (expanded navigation property) - try both camelCase and PascalCase
                                JsonElement historyArray;
                                if (orderElement.TryGetProperty("statusHistory", out historyArray) || orderElement.TryGetProperty("StatusHistory", out historyArray))
                                {
                                    foreach (var historyElement in historyArray.EnumerateArray())
                                    {
                                        order.StatusHistory.Add(new OrderStatusHistoryDto
                                        {
                                            Id = GetGuid(historyElement, "id", "Id"),
                                            Status = GetString(historyElement, "status", "Status") ?? "",
                                            Notes = GetString(historyElement, "notes", "Notes"),
                                            ChangedAt = GetDateTime(historyElement, "changedAt", "ChangedAt")
                                        });
                                    }
                                }

                                orders.Add(order);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Error parsing individual order from OData response");
                            }
                        }
                    }
                }

                if (odataResponse.TryGetProperty("@odata.count", out var countElement))
                {
                    totalCount = countElement.GetInt32();
                }
                else
                {
                    totalCount = orders.Count; // Fallback if count not available
                }

                _logger.LogInformation("OData response parsed: {OrderCount} orders, {TotalCount} total", orders.Count, totalCount);
                if (orders.Any())
                {
                    var firstOrder = orders.First();
                    _logger.LogInformation("First order sample: OrderId={OrderId}, Items.Count={ItemCount}, StatusHistory.Count={HistoryCount}",
                        firstOrder.OrderId, firstOrder.Items?.Count ?? 0, firstOrder.StatusHistory?.Count ?? 0);
                }

                var result = new PagedResult<OrderDto>(orders, totalCount);
                return JsonContent(JsonSerializer.Serialize(result, JsonOptions), 200);
            }
            catch (Exception parseEx)
            {
                _logger.LogError(parseEx, "Error parsing OData response. Content: {Content}", content.Substring(0, Math.Min(500, content.Length)));
                // Fallback: return the raw OData response
                return Content(content, "application/json");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching orders (paged)");
            return StatusCode(500, new { error = "Failed to fetch orders" });
        }
    }

    private async Task<HashSet<Guid>> GetAllowedRestaurantIdsAsync(bool isAdmin)
    {
        var ids = new HashSet<Guid>();

        try
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            _logger.LogInformation("GetAllowedRestaurantIdsAsync: isAdmin={IsAdmin}, UserId={UserId}", isAdmin, userIdClaim);

            var client = _httpClientFactory.CreateClient("RestaurantService");
            ForwardBearerToken(client);

            if (isAdmin)
            {
                var resp = await client.GetAsync("/api/restaurant/admin/all?skip=0&take=5000");
                var json = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GetAllowedRestaurantIdsAsync: Failed to fetch admin restaurants. Status={Status}, Response={Response}",
                        resp.StatusCode, json);
                    return ids;
                }

                var restaurants = JsonSerializer.Deserialize<List<RestaurantLight>>(json, JsonOptions) ?? new();
                _logger.LogInformation("GetAllowedRestaurantIdsAsync: Admin - found {Count} restaurants", restaurants.Count);
                foreach (var r in restaurants)
                {
                    if (r.RestaurantId != Guid.Empty) ids.Add(r.RestaurantId);
                }
                return ids;
            }
            else
            {
                var resp = await client.GetAsync("/api/restaurant/vendor/my-restaurants");
                var json = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GetAllowedRestaurantIdsAsync: Failed to fetch vendor restaurants. Status={Status}, Response={Response}",
                        resp.StatusCode, json);
                    return ids;
                }

                var restaurants = JsonSerializer.Deserialize<List<RestaurantLight>>(json, JsonOptions) ?? new();
                _logger.LogInformation("GetAllowedRestaurantIdsAsync: Vendor UserId={UserId} - found {Count} restaurants: {RestaurantIds}",
                    userIdClaim, restaurants.Count, string.Join(", ", restaurants.Select(r => r.RestaurantId)));
                foreach (var r in restaurants)
                {
                    if (r.RestaurantId != Guid.Empty) ids.Add(r.RestaurantId);
                }
                return ids;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving allowed restaurants");
            return ids;
        }
    }

    private async Task<List<OrderDto>> FetchOrdersForRestaurantsAsync(IEnumerable<Guid> restaurantIds)
    {
        var results = new List<OrderDto>();

        var client = _httpClientFactory.CreateClient("OrderService");
        ForwardBearerToken(client);

        foreach (var rid in restaurantIds)
        {
            try
            {
                var upstreamUrl = $"/api/Order/vendor/restaurants/{rid}/orders";
                var resp = await client.GetAsync(upstreamUrl);
                var json = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("OrderService returned {Status} for restaurant {RestaurantId}. Body: {Body}",
                        (int)resp.StatusCode, rid, json);
                    continue;
                }

                var list = JsonSerializer.Deserialize<List<OrderDto>>(json, JsonOptions) ?? new List<OrderDto>();
                results.AddRange(list);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed fetching orders for restaurant {RestaurantId}", rid);
            }
        }

        return results;
    }

    private static List<OrderDto> ApplySearch(List<OrderDto> orders, string? q)
    {
        if (string.IsNullOrWhiteSpace(q)) return orders;

        var term = q.Trim().ToLowerInvariant();

        return orders.Where(o =>
                o.OrderId.ToString().ToLowerInvariant().Contains(term) ||
                (o.Status ?? "").ToLowerInvariant().Contains(term) ||
                (o.DeliveryAddress ?? "").ToLowerInvariant().Contains(term) ||
                (o.Items ?? new List<OrderItemDto>()).Any(i => (i.Name ?? "").ToLowerInvariant().Contains(term))
            )
            .ToList();
    }

    private static List<OrderDto> ApplyOrderBy(List<OrderDto> orders, string? orderBy)
    {
        if (string.IsNullOrWhiteSpace(orderBy))
            return orders.OrderByDescending(o => o.CreatedAt).ToList();

        var parts = orderBy.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var field = parts.Length > 0 ? parts[0] : "CreatedAt";
        var desc = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        return field switch
        {
            "CreatedAt" => desc ? orders.OrderByDescending(o => o.CreatedAt).ToList()
                                : orders.OrderBy(o => o.CreatedAt).ToList(),

            "Total" => desc ? orders.OrderByDescending(o => o.Total).ToList()
                            : orders.OrderBy(o => o.Total).ToList(),

            "Status" => desc ? orders.OrderByDescending(o => o.Status).ToList()
                             : orders.OrderBy(o => o.Status).ToList(),

            _ => orders.OrderByDescending(o => o.CreatedAt).ToList()
        };
    }

    private sealed class RestaurantLight
    {
        public Guid RestaurantId { get; set; }
    }

    // ----------------------------
    // Search suggestions / Menu / Categories
    // ----------------------------

    [HttpGet("search-suggestions")]
    public async Task<IActionResult> GetSearchSuggestions([FromQuery] string query, [FromQuery] int maxResults = 10)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("RestaurantService");
            var response = await client.GetAsync($"/api/restaurant/search-suggestions?query={Uri.EscapeDataString(query)}&maxResults={maxResults}");
            var content = await response.Content.ReadAsStringAsync();
            return JsonContent(content, (int)response.StatusCode);
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
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return JsonContent(content, 200);
            }

            // For errors (including 500), return empty list instead of forwarding error
            // This allows the frontend to display "No menu items" instead of an error
            _logger.LogWarning("CatalogService returned error for restaurant menu: Status={StatusCode}, RestaurantId={RestaurantId}, Content={Content}",
                response.StatusCode, restaurantId, content);
            return JsonContent("[]", 200); // Return empty array
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "CatalogService unavailable when fetching restaurant menu, returning empty list");
            return JsonContent("[]", 200); // Return empty array
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching restaurant menu");
            return JsonContent("[]", 200); // Return empty array instead of 500
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
            {
                return JsonContent(content, 200);
            }

            // For errors, return empty list instead of forwarding error
            _logger.LogWarning("CatalogService returned error for categories: Status={StatusCode}, Content={Content}",
                response.StatusCode, content);
            return JsonContent("[]", 200); // Return empty array
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "CatalogService unavailable when fetching categories, returning empty list");
            return JsonContent("[]", 200); // Return empty array
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching categories");
            return JsonContent("[]", 200); // Return empty array instead of 500
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

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/order/cart");
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
            }

            var requestBody = new CreateCartRequest(request?.RestaurantId);

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            httpRequestMessage.Content = System.Net.Http.Json.JsonContent.Create(requestBody, options: jsonOptions);

            _logger.LogInformation("CreateCart: Forwarding to OrderService - URL={Url}, RestaurantId={RestaurantId}",
                httpRequestMessage.RequestUri, request?.RestaurantId);

            var response = await client.SendAsync(httpRequestMessage);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var result = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);
                    if (result.TryGetProperty("cartId", out var cartIdElement))
                    {
                        if (Guid.TryParse(cartIdElement.GetString(), out var cartId))
                        {
                            try
                            {
                                var sessionId = await GetOrCreateSessionIdAsync();
                                await _cartSessionService.StoreCartIdForSessionAsync(sessionId, cartId);
                            }
                            catch (Exception redisEx)
                            {
                                _logger.LogWarning(redisEx, "Redis unavailable when storing cart session, continuing");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse cartId from response, but cart was created");
                }

                return JsonContent(content, 200);
            }
            else
            {
                _logger.LogWarning("OrderService returned error: {StatusCode}, {Content}", response.StatusCode, content);
                // Return error response from OrderService, but ensure it's valid JSON
                try
                {
                    // Try to parse as JSON to ensure it's valid
                    JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);
                    return JsonContent(content, (int)response.StatusCode);
                }
                catch
                {
                    // If not valid JSON, return a proper error object
                    return StatusCode((int)response.StatusCode, new { error = "Failed to create cart", message = content });
                }
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "OrderService unavailable when creating cart");
            return StatusCode(503, new { error = "Cart service unavailable", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating cart: {Message}", ex.Message);
            return StatusCode(500, new { error = "Failed to create cart", message = ex.Message });
        }
    }

    [HttpGet("cart")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCart()
    {
        try
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                try
                {
                    var client = _httpClientFactory.CreateClient("OrderService");

                    var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/order/cart");
                    if (Request.Headers.TryGetValue("Authorization", out var authHeader))
                    {
                        httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
                    }

                    var response = await client.SendAsync(httpRequestMessage);
                    var content = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                        return JsonContent(content, 200);

                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        return Ok((object?)null);

                    _logger.LogWarning("OrderService returned error for authenticated cart: Status={StatusCode}, Content={Content}",
                        response.StatusCode, content);
                    return JsonContent(content, (int)response.StatusCode);
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "OrderService unavailable when getting authenticated cart");
                    return Ok((object?)null);
                }
            }

            // guest user
            try
            {
                string sessionId;
                try
                {
                    sessionId = await GetOrCreateSessionIdAsync();
                }
                catch (Exception sessionEx)
                {
                    _logger.LogWarning(sessionEx, "Failed to get or create session ID, returning empty cart");
                    return Ok((object?)null);
                }

                Guid? cartId;
                try
                {
                    cartId = await _cartSessionService.GetCartIdForSessionAsync(sessionId);
                }
                catch (Exception redisEx)
                {
                    _logger.LogWarning(redisEx, "Redis unavailable when getting cart ID, returning empty cart");
                    return Ok((object?)null);
                }

                if (cartId.HasValue)
                {
                    try
                    {
                        var client = _httpClientFactory.CreateClient("OrderService");
                        var response = await client.GetAsync($"/api/order/cart/{cartId.Value}");
                        var content = await response.Content.ReadAsStringAsync();

                        if (response.IsSuccessStatusCode)
                            return JsonContent(content, 200);

                        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            try { await _cartSessionService.ClearSessionCartAsync(sessionId); } catch { }
                            return Ok((object?)null);
                        }

                        // For any other error (including 500), log and return empty cart instead of forwarding error
                        _logger.LogWarning("OrderService returned error for guest cart: Status={StatusCode}, Content={Content}",
                            response.StatusCode, content);
                        return Ok((object?)null);
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger.LogError(ex, "OrderService unavailable when getting guest cart");
                        return Ok((object?)null);
                    }
                }
            }
            catch (Exception redisEx)
            {
                _logger.LogWarning(redisEx, "Redis unavailable when getting cart, returning empty cart");
                return Ok((object?)null);
            }

            return Ok((object?)null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting cart: {Message}", ex.Message);
            // Return empty cart instead of 500 to prevent frontend errors
            return Ok((object?)null);
        }
    }

    [HttpGet("cart/{cartId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCartById(Guid cartId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("OrderService");
            var response = await client.GetAsync($"/api/order/cart/{cartId}");
            var content = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return Ok((object?)null);

            return JsonContent(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cart by ID: {Message}", ex.Message);
            return StatusCode(500, new { error = $"Failed to get cart: {ex.Message}" });
        }
    }

    [HttpPost("cart/{cartId}/items")]
    [AllowAnonymous]
    public async Task<IActionResult> AddItemToCart(Guid cartId, [FromBody] AddCartItemRequest request)
    {
        try
        {
            if (request == null)
                return BadRequest(new { error = "Request body is required" });

            var client = _httpClientFactory.CreateClient("OrderService");

            var orderServiceRequest = new
            {
                MenuItemId = request.MenuItemId,
                Name = request.Name,
                Price = request.Price,
                Quantity = request.Quantity,
                Options = request.Options
            };

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/order/cart/{cartId}/items");
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
            }
            httpRequestMessage.Content = System.Net.Http.Json.JsonContent.Create(orderServiceRequest, options: jsonOptions);

            var response = await client.SendAsync(httpRequestMessage);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                if (User.Identity?.IsAuthenticated != true)
                {
                    try
                    {
                        var sessionId = await GetOrCreateSessionIdAsync();
                        var existingCartId = await _cartSessionService.GetCartIdForSessionAsync(sessionId);
                        if (!existingCartId.HasValue || existingCartId.Value != cartId)
                        {
                            await _cartSessionService.StoreCartIdForSessionAsync(sessionId, cartId);
                        }
                    }
                    catch (Exception redisEx)
                    {
                        _logger.LogWarning(redisEx, "Redis unavailable when storing cart session, continuing");
                    }
                }

                return Ok(new { success = true, cartId });
            }

            // For errors, ensure we return valid JSON
            _logger.LogWarning("OrderService returned error when adding item to cart: Status={StatusCode}, Content={Content}",
                response.StatusCode, content);
            try
            {
                // Try to parse as JSON to ensure it's valid
                JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);
                return JsonContent(content, (int)response.StatusCode);
            }
            catch
            {
                // If not valid JSON, return a proper error object
                return StatusCode((int)response.StatusCode, new { error = "Failed to add item to cart", message = content });
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "OrderService unavailable when adding item to cart");
            return StatusCode(503, new { error = "Cart service unavailable", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AddItemToCart error: {Message}", ex.Message);
            return StatusCode(500, new { error = "Failed to add item to cart", message = ex.Message });
        }
    }

    [HttpPut("cart/{cartId}/items/{cartItemId}")]
    [AllowAnonymous]
    public async Task<IActionResult> UpdateCartItem(Guid cartId, Guid cartItemId, [FromBody] UpdateCartItemRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("OrderService");
            var response = await client.PutAsJsonAsync($"/api/order/cart/{cartId}/items/{cartItemId}", request);
            var content = await response.Content.ReadAsStringAsync();
            return JsonContent(content, (int)response.StatusCode);
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
            var client = _httpClientFactory.CreateClient("OrderService");
            var response = await client.DeleteAsync($"/api/order/cart/{cartId}/items/{cartItemId}");
            var content = await response.Content.ReadAsStringAsync();
            return JsonContent(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cart item");
            return StatusCode(500, new { error = "Failed to remove cart item" });
        }
    }

    // ----------------------------
    // Orders place / get order
    // ----------------------------

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
                            return BadRequest(new { error = "This restaurant is not set up to accept payments yet. Please contact the restaurant directly or try again later." });
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
            var orderRequest = new HttpRequestMessage(HttpMethod.Post, "/api/order/place");
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
                orderRequest.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
            orderRequest.Content = System.Net.Http.Json.JsonContent.Create(request);

            var orderResponse = await orderClient.SendAsync(orderRequest);
            var orderContent = await orderResponse.Content.ReadAsStringAsync();
            if (!orderResponse.IsSuccessStatusCode)
                return JsonContent(orderContent, (int)orderResponse.StatusCode);

            // Parse orderId from { "orderId": "..." }
            Guid orderId;
            try
            {
                var orderJson = System.Text.Json.JsonDocument.Parse(orderContent);
                var orderIdEl = orderJson.RootElement.TryGetProperty("orderId", out var o) ? o : orderJson.RootElement.GetProperty("OrderId");
                orderId = Guid.Parse(orderIdEl.GetString()!);
            }
            catch
            {
                return JsonContent(orderContent, (int)orderResponse.StatusCode);
            }

            // Get order details (total, serviceFee, restaurantId) for checkout session
            var getOrderRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/order/{orderId}");
            if (Request.Headers.TryGetValue("Authorization", out var getOrderAuthHeader))
                getOrderRequest.Headers.TryAddWithoutValidation("Authorization", getOrderAuthHeader.ToString());
            var getOrderResponse = await orderClient.SendAsync(getOrderRequest);
            if (!getOrderResponse.IsSuccessStatusCode)
                return Ok(new { orderId, checkoutUrl = (string?)null, error = "Order placed but could not load details for payment" });

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

            // Create Stripe Checkout session (destination charge + app fee + manual capture)
            var baseUrl = _configuration["AppBaseUrl"] ?? Request.Scheme + "://" + Request.Host;
            var successUrl = baseUrl.TrimEnd('/') + "/orders?payment=success";
            var cancelUrl = baseUrl.TrimEnd('/') + "/cart?payment=cancelled";
            var checkoutPaymentClient = _httpClientFactory.CreateClient("PaymentService");
            var checkoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/payment/checkout/session");
            if (Request.Headers.TryGetValue("Authorization", out var checkoutAuthHeader))
                checkoutRequest.Headers.TryAddWithoutValidation("Authorization", checkoutAuthHeader.ToString());
            checkoutRequest.Content = System.Net.Http.Json.JsonContent.Create(new
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
            errorMessage ??= "This restaurant is not set up to accept payments yet. Your order has been placed, but payment cannot be processed. Please contact the restaurant directly.";

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
            return JsonContent(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vendor Stripe onboarding status");
            return StatusCode(500, new { error = "Failed to fetch onboarding status" });
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
            return JsonContent(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking restaurant payment readiness");
            return StatusCode(500, new { error = "Failed to check payment readiness" });
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
            return JsonContent(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating vendor Stripe connect link");
            return StatusCode(500, new { error = "Failed to create Stripe connect link" });
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
            {
                httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
            }

            var response = await client.SendAsync(httpRequestMessage);
            var content = await response.Content.ReadAsStringAsync();
            return JsonContent(content, (int)response.StatusCode);
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

    // ----------------------------
    // Vendor restaurants
    // ----------------------------

    [HttpGet("vendor/my-restaurants")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> GetMyRestaurants()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("RestaurantService");
            ForwardBearerToken(client);

            var response = await client.GetAsync("/api/restaurant/vendor/my-restaurants");
            var content = await response.Content.ReadAsStringAsync();
            return JsonContent(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vendor restaurants");
            return StatusCode(500, new { error = "Failed to fetch vendor restaurants" });
        }
    }

    [HttpPost("vendor/restaurants")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> CreateRestaurant([FromBody] object? request)
    {
        if (request == null)
        {
            return BadRequest(new { message = "Request body is required" });
        }
        try
        {
            var client = _httpClientFactory.CreateClient("RestaurantService");
            ForwardBearerToken(client);

            var json = JsonSerializer.Serialize(request, JsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/restaurant", content);

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonContent(responseContent, (int)response.StatusCode);
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
            var client = _httpClientFactory.CreateClient("RestaurantService");
            ForwardBearerToken(client);

            var json = JsonSerializer.Serialize(request, JsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PutAsync($"/api/restaurant/{restaurantId}", content);

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonContent(responseContent, (int)response.StatusCode);
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
            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonContent(responseContent, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting restaurant");
            return StatusCode(500, new { error = "Failed to delete restaurant" });
        }
    }

    // ----------------------------
    // Admin restaurants
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
            return JsonContent(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching all restaurants");
            return StatusCode(500, new { error = "Failed to fetch all restaurants" });
        }
    }

    [HttpPost("admin/restaurants")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminCreateRestaurant([FromBody] object? request)
    {
        if (request == null)
        {
            return BadRequest(new { message = "Request body is required" });
        }
        try
        {
            var client = _httpClientFactory.CreateClient("RestaurantService");
            ForwardBearerToken(client);

            var json = JsonSerializer.Serialize(request, JsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/restaurant", content);

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonContent(responseContent, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating restaurant (admin)");
            return StatusCode(500, new { error = "Failed to create restaurant" });
        }
    }

    [HttpPut("admin/restaurants/{restaurantId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminUpdateRestaurant(Guid restaurantId, [FromBody] object request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("RestaurantService");
            ForwardBearerToken(client);

            var json = JsonSerializer.Serialize(request, JsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PutAsync($"/api/restaurant/admin/{restaurantId}", content);

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonContent(responseContent, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating restaurant (admin)");
            return StatusCode(500, new { error = "Failed to update restaurant" });
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
            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonContent(responseContent, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting restaurant (admin)");
            return StatusCode(500, new { error = "Failed to delete restaurant" });
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

            var json = JsonSerializer.Serialize(request, JsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PatchAsync($"/api/restaurant/admin/{restaurantId}/status", content);

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonContent(responseContent, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling restaurant status (admin)");
            return StatusCode(500, new { error = "Failed to toggle restaurant status" });
        }
    }

    // ----------------------------
    // Admin roles
    // ----------------------------

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

            return JsonContent(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning role");
            return StatusCode(500, new { error = "Failed to assign role" });
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

            return JsonContent(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking role");
            return StatusCode(500, new { error = "Failed to revoke role" });
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

            return JsonContent(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user roles");
            return StatusCode(500, new { error = "Failed to get user roles" });
        }
    }

    // ----------------------------
    // Vendor Orders passthrough (keep as-is)
    // ----------------------------

    [HttpGet("vendor/restaurants/{restaurantId}/orders")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> GetVendorOrders(Guid restaurantId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("OrderService");
            ForwardBearerToken(client);

            var response = await client.GetAsync($"/api/Order/vendor/restaurants/{restaurantId}/orders");
            var content = await response.Content.ReadAsStringAsync();

            return JsonContent(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vendor orders");
            return StatusCode(500, new { error = "Failed to fetch vendor orders" });
        }
    }

    // ----------------------------
    // Menu Items (CatalogService passthrough)
    // ----------------------------

    [HttpPost("restaurants/{restaurantId}/menu-items")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> CreateMenuItem(Guid restaurantId, [FromBody] object request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("CatalogService");
            ForwardBearerToken(client);

            var json = JsonSerializer.Serialize(request, JsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            // CatalogController: POST /api/Catalog/restaurants/{restaurantId}/menu-items
            var response = await client.PostAsync($"/api/Catalog/restaurants/{restaurantId}/menu-items", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            return JsonContent(responseContent, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating menu item for restaurant {RestaurantId}", restaurantId);
            return StatusCode(500, new { message = "Failed to create menu item" });
        }
    }

    [HttpPut("menu-items/{menuItemId}")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> UpdateMenuItem(Guid menuItemId, [FromBody] object request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("CatalogService");
            ForwardBearerToken(client);

            var json = JsonSerializer.Serialize(request, JsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            // CatalogController: PUT /api/Catalog/menu-items/{menuItemId}
            var response = await client.PutAsync($"/api/Catalog/menu-items/{menuItemId}", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            return JsonContent(responseContent, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating menu item {MenuItemId}", menuItemId);
            return StatusCode(500, new { message = "Failed to update menu item" });
        }
    }

    [HttpPatch("menu-items/{menuItemId}/availability")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> SetMenuItemAvailability(Guid menuItemId, [FromBody] object request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("CatalogService");
            ForwardBearerToken(client);

            var json = JsonSerializer.Serialize(request, JsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            // CatalogController: PATCH /api/Catalog/menu-items/{menuItemId}/availability
            var response = await client.PatchAsync($"/api/Catalog/menu-items/{menuItemId}/availability", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            return JsonContent(responseContent, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting availability for menu item {MenuItemId}", menuItemId);
            return StatusCode(500, new { message = "Failed to update menu item availability" });
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

            return JsonContent(content, (int)response.StatusCode);
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

            return JsonContent(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread count for order {OrderId}", orderId);
            return StatusCode(500, new { error = "Failed to get unread count" });
        }
    }


    [HttpPut("orders/{orderId}/status")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> UpdateOrderStatus(Guid orderId, [FromBody] UpdateOrderStatusRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("OrderService");
            ForwardBearerToken(client);

            var response = await client.PutAsJsonAsync($"/api/Order/{orderId}/status", request);
            var content = await response.Content.ReadAsStringAsync();

            return JsonContent(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order status");
            return StatusCode(500, new { error = "Failed to update order status" });
        }
    }

    // ----------------------------
    // Auth passthrough
    // ----------------------------

    [HttpPost("auth/register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("IdentityService");
            var response = await client.PostAsJsonAsync("/api/auth/register", request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("IdentityService registration failed: Status={StatusCode}, Content={Content}",
                    response.StatusCode, content);
            }

            return JsonContent(content, (int)response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during registration - IdentityService may be unreachable. URL: {Url}",
                _configuration["Services:IdentityService"] ?? "http://identity-service:5000");
            return StatusCode(500, new { error = "Registration service unavailable", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration: {Message}", ex.Message);
            return StatusCode(500, new { error = "Failed to register", message = ex.Message });
        }
    }

    [HttpPost("auth/login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { error = "Email and password are required" });

            var guestSessionId = await GetOrCreateSessionIdAsync();
            var guestCartId = await _cartSessionService.GetCartIdForSessionAsync(guestSessionId);

            var client = _httpClientFactory.CreateClient("IdentityService");
            var response = await client.PostAsJsonAsync("/api/auth/login", request);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                // optional: merge carts (your existing logic kept)
                try
                {
                    var loginResponse = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);
                    string? userIdString = null;

                    if (loginResponse.TryGetProperty("userId", out var userIdElement))
                        userIdString = userIdElement.GetString();
                    else if (loginResponse.TryGetProperty("user", out var userElement) && userElement.TryGetProperty("id", out var idElement))
                        userIdString = idElement.GetString();
                    else if (loginResponse.TryGetProperty("id", out var idElement2))
                        userIdString = idElement2.GetString();

                    if (!string.IsNullOrEmpty(userIdString) && Guid.TryParse(userIdString, out var userId) && guestCartId.HasValue)
                    {
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
                            var mergedCart = JsonSerializer.Deserialize<JsonElement>(mergeContent, JsonOptions);
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
                            _logger.LogWarning("Cart merge failed, transferred guest cart to user");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse login response or merge carts, continuing with login");
                }
            }

            return JsonContent(content, (int)response.StatusCode);
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
            var content = await response.Content.ReadAsStringAsync();
            return JsonContent(content, (int)response.StatusCode);
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
            return JsonContent(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, new { error = "Failed to logout" });
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
            return JsonContent(content, (int)response.StatusCode);
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
            return JsonContent(content, (int)response.StatusCode);
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
    // Reviews
    // ----------------------------

    [HttpPost("reviews")]
    [Authorize]
    public async Task<IActionResult> CreateReview([FromBody] CreateReviewRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("ReviewService");
            ForwardBearerToken(client);

            var response = await client.PostAsJsonAsync("/api/review", request);
            var content = await response.Content.ReadAsStringAsync();
            return JsonContent(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating review");
            return StatusCode(500, new { message = "Failed to create review" });
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
            return JsonContent(content, (int)response.StatusCode);
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
            return JsonContent(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting restaurant reviews");
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
            return JsonContent(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting restaurant rating");
            return StatusCode(500, new { message = "Failed to get rating" });
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
            return JsonContent(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user reviews");
            return StatusCode(500, new { message = "Failed to get reviews" });
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
            return JsonContent(content, (int)response.StatusCode);
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
            return JsonContent(content, (int)response.StatusCode);
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
                
                return JsonContent(responseContent, (int)response.StatusCode);
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
            return JsonContent(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vendor documents");
            return StatusCode(500, new { message = "Failed to fetch documents" });
        }
    }

    [HttpGet("documents/{documentId}")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> GetDocument(Guid documentId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DocumentService");
            ForwardBearerToken(client);

            var response = await client.GetAsync($"/api/document/{documentId}");
            var content = await response.Content.ReadAsStringAsync();
            return JsonContent(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching document");
            return StatusCode(500, new { message = "Failed to fetch document" });
        }
    }

    [HttpPatch("documents/{documentId}/status")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> UpdateDocumentStatus(Guid documentId, [FromBody] UpdateDocumentStatusRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DocumentService");
            ForwardBearerToken(client);

            var response = await client.PatchAsJsonAsync($"/api/document/{documentId}/status", request);
            var content = await response.Content.ReadAsStringAsync();
            return JsonContent(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating document status");
            return StatusCode(500, new { message = "Failed to update document status" });
        }
    }

    // Admin document endpoints (more specific routes first)
    [HttpGet("documents/admin/all")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllDocuments(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100,
        [FromQuery] Guid? vendorId = null,
        [FromQuery] bool? isActive = null)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DocumentService");
            ForwardBearerToken(client);

            var queryParams = new List<string>();
            if (skip > 0) queryParams.Add($"skip={skip}");
            if (take != 100) queryParams.Add($"take={take}");
            if (vendorId.HasValue) queryParams.Add($"vendorId={vendorId.Value}");
            if (isActive.HasValue) queryParams.Add($"isActive={isActive.Value}");

            var queryString = queryParams.Any() ? "?" + string.Join("&", queryParams) : "";
            var response = await client.GetAsync($"/api/document/admin/all{queryString}");
            var content = await response.Content.ReadAsStringAsync();
            return JsonContent(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching all documents");
            return StatusCode(500, new { message = "Failed to fetch documents" });
        }
    }

    [HttpPatch("documents/admin/{documentId}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminUpdateDocumentStatus(Guid documentId, [FromBody] UpdateDocumentStatusRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DocumentService");
            ForwardBearerToken(client);

            var response = await client.PatchAsJsonAsync($"/api/document/admin/{documentId}/status", request);
            var content = await response.Content.ReadAsStringAsync();
            return JsonContent(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating document status");
            return StatusCode(500, new { message = "Failed to update document status" });
        }
    }

    [HttpDelete("documents/admin/{documentId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminDeleteDocument(Guid documentId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DocumentService");
            ForwardBearerToken(client);

            var response = await client.DeleteAsync($"/api/document/admin/{documentId}");
            var content = await response.Content.ReadAsStringAsync();
            return JsonContent(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document");
            return StatusCode(500, new { message = "Failed to delete document" });
        }
    }

    [HttpDelete("documents/{documentId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteDocument(Guid documentId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DocumentService");
            ForwardBearerToken(client);

            var response = await client.DeleteAsync($"/api/document/{documentId}");
            var content = await response.Content.ReadAsStringAsync();
            return JsonContent(content, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document");
            return StatusCode(500, new { message = "Failed to delete document" });
        }
    }
}

// ----------------------------
// DTOs / Records
// ----------------------------

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);
public record UpdateDocumentStatusRequest(bool IsActive);

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
    string? SpecialInstructions,
    string? IdempotencyKey
);

public record RegisterRequest(string FirstName, string LastName, string? DisplayName, string Email, string PhoneNumber, string Password, string? Role);
public record LoginRequest(string Email, string Password);
public record RefreshTokenRequest(string RefreshToken);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Token, string Email, string NewPassword, string ConfirmPassword);
public record AssignRoleRequest(string Email, string Role);
public record RevokeRoleRequest(string Email, string Role);
public record UpdateOrderStatusRequest(string Status, string? Notes);
public record CreateReviewRequest(Guid OrderId, Guid RestaurantId, CreateReviewDto Review);
public record CreateReviewDto(int Rating, string? Comment, List<string>? Tags);
public record UpdateReviewDto(int? Rating, string? Comment, List<string>? Tags);
public record AddResponseRequest(string Response);

// Keep this in BFF so it can page/filter/sort vendor orders
public class OrderDto
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid RestaurantId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? SpecialInstructions { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal Total { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
    public List<OrderStatusHistoryDto> StatusHistory { get; set; } = new();
}

public class OrderItemDto
{
    public Guid OrderItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal TotalPrice { get; set; }
}

public class OrderStatusHistoryDto
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime ChangedAt { get; set; }
}
