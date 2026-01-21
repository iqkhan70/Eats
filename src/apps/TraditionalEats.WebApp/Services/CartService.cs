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
                Console.WriteLine("Cart not found (404)");
                return null;
            }
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error getting cart: {response.StatusCode} - {errorContent}");
                return null;
            }
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Cart response content: {content}");
            if (string.IsNullOrWhiteSpace(content) || content == "null")
            {
                Console.WriteLine("Cart response is null or empty");
                return null;
            }
            var cart = await response.Content.ReadFromJsonAsync<CartDto>();
            Console.WriteLine($"Deserialized cart: CartId={cart?.CartId}, Items.Count={cart?.Items?.Count ?? 0}");
            if (cart != null && cart.Items != null)
            {
                foreach (var item in cart.Items)
                {
                    Console.WriteLine($"  Item: {item.Name}, Quantity: {item.Quantity}, Price: {item.UnitPrice}");
                }
            }
            return cart;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception getting cart: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
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

    public async Task<Guid> PlaceOrderAsync(Guid cartId, string deliveryAddress)
    {
        var request = new
        {
            CartId = cartId,
            DeliveryAddress = deliveryAddress,
            IdempotencyKey = (string?)null
        };
        var response = await _httpClient.PostAsJsonAsync("WebBff/orders/place", request);
        var result = await response.Content.ReadFromJsonAsync<PlaceOrderResponse>();
        return result?.OrderId ?? Guid.Empty;
    }

    private class CreateCartResponse
    {
        public Guid CartId { get; set; }
    }

    private class PlaceOrderResponse
    {
        public Guid OrderId { get; set; }
    }
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
