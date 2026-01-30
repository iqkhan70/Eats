using System.Net.Http.Json;

namespace TraditionalEats.WebApp.Services;

public class CartService
{
    private readonly HttpClient _httpClient;

    public CartService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CartDto?> GetCartAsync()
    {
        try
        {
            // BFF handles session management via cookies and Redis
            var response = await _httpClient.GetAsync("WebBff/cart");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Cart doesn't exist yet - this is normal for new users
                return null;
            }
            if (!response.IsSuccessStatusCode)
            {
                // Silently handle errors (e.g., Redis connection issues)
                // The cart count will just show 0, which is acceptable
                return null;
            }
            var content = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(content) || content == "null")
            {
                return null;
            }
            var cart = await response.Content.ReadFromJsonAsync<CartDto>();
            return cart;
        }
        catch
        {
            // Silently handle exceptions (e.g., network errors, Redis connection issues)
            // The cart count will just show 0, which is acceptable
            return null;
        }
    }

    public async Task<Guid> CreateCartAsync(Guid? restaurantId = null)
    {
        var request = new { RestaurantId = restaurantId };
        var response = await _httpClient.PostAsJsonAsync("WebBff/cart", request);
        var result = await response.Content.ReadFromJsonAsync<CreateCartResponse>();
        // BFF stores cartId in Redis session automatically
        return result?.CartId ?? Guid.Empty;
    }

    public async Task<HttpResponseMessage> AddItemToCartAsync(Guid cartId, Guid menuItemId, string name, decimal price, int quantity = 1)
    {
        var request = new
        {
            MenuItemId = menuItemId,
            Name = name,
            Price = price,
            Quantity = quantity,
            Options = (Dictionary<string, string>?)null
        };
        return await _httpClient.PostAsJsonAsync($"WebBff/cart/{cartId}/items", request);
    }

    public async Task UpdateCartItemQuantityAsync(Guid cartId, Guid cartItemId, int quantity)
    {
        var request = new { Quantity = quantity };
        var response = await _httpClient.PutAsJsonAsync($"WebBff/cart/{cartId}/items/{cartItemId}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveCartItemAsync(Guid cartId, Guid cartItemId)
    {
        await _httpClient.DeleteAsync($"WebBff/cart/{cartId}/items/{cartItemId}");
    }

    public async Task ClearCartAsync(Guid cartId)
    {
        await _httpClient.DeleteAsync($"WebBff/cart/{cartId}");
    }

    public async Task<PlaceOrderResult> PlaceOrderAsync(Guid cartId, string deliveryAddress, string? specialInstructions = null)
    {
        var request = new
        {
            CartId = cartId,
            DeliveryAddress = deliveryAddress,
            SpecialInstructions = specialInstructions,
            IdempotencyKey = (string?)null
        };
        var response = await _httpClient.PostAsJsonAsync("WebBff/orders/place", request);
        var result = await response.Content.ReadFromJsonAsync<PlaceOrderResponse>();
        return new PlaceOrderResult
        {
            OrderId = result?.OrderId ?? Guid.Empty,
            CheckoutUrl = result?.CheckoutUrl,
            Error = result?.Error
        };
    }

    private class CreateCartResponse
    {
        public Guid CartId { get; set; }
    }

    private class PlaceOrderResponse
    {
        public Guid OrderId { get; set; }
        public string? CheckoutUrl { get; set; }
        public string? Error { get; set; }
    }
}

public class PlaceOrderResult
{
    public Guid OrderId { get; set; }
    public string? CheckoutUrl { get; set; }
    public string? Error { get; set; }
}

public class CartDto
{
    public Guid CartId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? RestaurantId { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal Total { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<CartItemDto> Items { get; set; } = new();
}

public class CartItemDto
{
    public Guid CartItemId { get; set; }
    public Guid CartId { get; set; }
    public Guid MenuItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? SelectedOptionsJson { get; set; }
}
