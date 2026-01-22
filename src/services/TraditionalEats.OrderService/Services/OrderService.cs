using Microsoft.EntityFrameworkCore;
using TraditionalEats.BuildingBlocks.Messaging;
using TraditionalEats.BuildingBlocks.Redis;
using TraditionalEats.Contracts.Events;
using TraditionalEats.OrderService.Data;
using TraditionalEats.OrderService.Entities;

namespace TraditionalEats.OrderService.Services;

public interface IOrderService
{
    Task<Guid> CreateCartAsync(Guid? customerId, Guid? restaurantId = null);
    Task<Cart?> GetCartAsync(Guid cartId);
    Task<Cart?> GetCartByCustomerAsync(Guid customerId);
    Task AddItemToCartAsync(Guid cartId, Guid menuItemId, string name, decimal price, int quantity, Dictionary<string, string>? options);
    Task UpdateCartItemQuantityAsync(Guid cartId, Guid cartItemId, int quantity);
    Task RemoveCartItemAsync(Guid cartId, Guid cartItemId);
    Task ClearCartAsync(Guid cartId);
    Task<Guid> MergeCartsAsync(Guid guestCartId, Guid userCartId);
    Task<Guid> PlaceOrderAsync(Guid cartId, Guid customerId, string deliveryAddress, string idempotencyKey);
    Task<Order?> GetOrderAsync(Guid orderId);
    Task<List<Order>> GetOrdersByCustomerAsync(Guid customerId);
    Task<bool> UpdateOrderStatusAsync(Guid orderId, string newStatus, string? notes = null);
}

public class OrderService : IOrderService
{
    private readonly OrderDbContext _context;
    private readonly IRedisService _redis;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        OrderDbContext context,
        IRedisService redis,
        IMessagePublisher messagePublisher,
        ILogger<OrderService> logger)
    {
        _context = context;
        _redis = redis;
        _messagePublisher = messagePublisher;
        _logger = logger;
    }

    public async Task<Guid> CreateCartAsync(Guid? customerId, Guid? restaurantId = null)
    {
        var cartId = Guid.NewGuid();
        var cart = new Cart
        {
            CartId = cartId,
            CustomerId = customerId,
            RestaurantId = restaurantId,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Carts.Add(cart);
        await _context.SaveChangesAsync();

        // Cache cart
        await _redis.SetAsync($"cart:{cartId}", cart, TimeSpan.FromHours(1));

        return cartId;
    }

    public async Task<Cart?> GetCartAsync(Guid cartId)
    {
        // Try cache first
        var cached = await _redis.GetAsync<Cart>($"cart:{cartId}");
        if (cached != null)
            return cached;

        var cart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CartId == cartId);

        if (cart != null)
        {
            await _redis.SetAsync($"cart:{cartId}", cart, TimeSpan.FromHours(1));
        }

        return cart;
    }

    public async Task<Cart?> GetCartByCustomerAsync(Guid customerId)
    {
        var cart = await _context.Carts
            .Include(c => c.Items)
            .Where(c => c.CustomerId == customerId)
            .OrderByDescending(c => c.UpdatedAt)
            .FirstOrDefaultAsync();

        if (cart != null)
        {
            await _redis.SetAsync($"cart:{cart.CartId}", cart, TimeSpan.FromHours(1));
        }

        return cart;
    }

    public async Task AddItemToCartAsync(Guid cartId, Guid menuItemId, string name, decimal price, int quantity, Dictionary<string, string>? options)
    {
        // Always load with tracking to ensure EF Core can detect changes
        var cart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CartId == cartId);
        
        if (cart == null)
            throw new InvalidOperationException("Cart not found");

        // Check if item already exists in the tracked cart
        var existingItem = cart.Items.FirstOrDefault(i => i.MenuItemId == menuItemId);
        
        if (existingItem != null)
        {
            // Item exists - update quantity
            _logger.LogInformation("Updating existing cart item: MenuItemId={MenuItemId}, CurrentQuantity={CurrentQuantity}, AddingQuantity={AddingQuantity}", 
                menuItemId, existingItem.Quantity, quantity);
            existingItem.Quantity += quantity;
            existingItem.TotalPrice = existingItem.Quantity * existingItem.UnitPrice;
        }
        else
        {
            // Item doesn't exist - add new one
            _logger.LogInformation("Adding new cart item: MenuItemId={MenuItemId}, Quantity={Quantity}", menuItemId, quantity);
            var cartItem = new CartItem
            {
                CartItemId = Guid.NewGuid(),
                CartId = cartId,
                MenuItemId = menuItemId,
                Name = name,
                Quantity = quantity,
                UnitPrice = price,
                TotalPrice = price * quantity,
                SelectedOptionsJson = options != null ? System.Text.Json.JsonSerializer.Serialize(options) : null
            };
            _context.CartItems.Add(cartItem);
            cart.Items.Add(cartItem);
        }

        RecalculateCartTotals(cart);
        cart.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        
        // Invalidate cache and reload cart to get latest state
        await _redis.DeleteAsync($"cart:{cartId}");
        
        // Reload cart from database to get the latest state for caching
        cart = await _context.Carts
            .Include(c => c.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CartId == cartId);
        
        if (cart != null)
        {
            await _redis.SetAsync($"cart:{cartId}", cart, TimeSpan.FromHours(1));
        }
    }

    public async Task UpdateCartItemQuantityAsync(Guid cartId, Guid cartItemId, int quantity)
    {
        // Always get from database to ensure EF Core tracking
        var cart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CartId == cartId);
        
        if (cart == null)
            throw new InvalidOperationException("Cart not found");

        // Reload items collection to ensure we have the latest data
        await _context.Entry(cart).Collection(c => c.Items).LoadAsync();

        var item = cart.Items.FirstOrDefault(i => i.CartItemId == cartItemId);
        if (item == null)
            throw new InvalidOperationException("Cart item not found");

        if (quantity <= 0)
        {
            await RemoveCartItemAsync(cartId, cartItemId);
            return;
        }

        _logger.LogInformation("Updating cart item quantity: CartItemId={CartItemId}, OldQuantity={OldQuantity}, NewQuantity={NewQuantity}", 
            cartItemId, item.Quantity, quantity);

        item.Quantity = quantity;
        item.TotalPrice = item.Quantity * item.UnitPrice;

        RecalculateCartTotals(cart);
        cart.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        
        // Invalidate cache to ensure fresh data on next read
        await _redis.DeleteAsync($"cart:{cartId}");
        
        // Reload cart from database to get the latest state
        cart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CartId == cartId);
        
        if (cart != null)
        {
            await _redis.SetAsync($"cart:{cartId}", cart, TimeSpan.FromHours(1));
        }
    }

    public async Task RemoveCartItemAsync(Guid cartId, Guid cartItemId)
    {
        var cart = await GetCartAsync(cartId);
        if (cart == null)
            throw new InvalidOperationException("Cart not found");

        var item = cart.Items.FirstOrDefault(i => i.CartItemId == cartItemId);
        if (item != null)
        {
            cart.Items.Remove(item);
            _context.CartItems.Remove(item);

            RecalculateCartTotals(cart);
            cart.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await _redis.SetAsync($"cart:{cartId}", cart, TimeSpan.FromHours(1));
        }
    }

    public async Task ClearCartAsync(Guid cartId)
    {
        var cart = await GetCartAsync(cartId);
        if (cart == null)
            throw new InvalidOperationException("Cart not found");

        cart.Items.Clear();
        RecalculateCartTotals(cart);
        cart.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await _redis.SetAsync($"cart:{cartId}", cart, TimeSpan.FromHours(1));
    }

    public async Task<Guid> MergeCartsAsync(Guid guestCartId, Guid userCartId)
    {
        _logger.LogInformation("Merging guest cart {GuestCartId} into user cart {UserCartId}", guestCartId, userCartId);
        
        var guestCart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CartId == guestCartId);
        
        var userCart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CartId == userCartId);

        if (guestCart == null)
        {
            _logger.LogWarning("Guest cart {GuestCartId} not found, returning user cart {UserCartId}", guestCartId, userCartId);
            return userCartId;
        }

        if (userCart == null)
        {
            _logger.LogWarning("User cart {UserCartId} not found, transferring guest cart {GuestCartId} to user", userCartId, guestCartId);
            // Transfer guest cart to user - just return the guest cart ID
            // The caller will update the customerId in the database
            return guestCartId;
        }

        // If carts are from different restaurants, prefer the most recently updated one
        if (guestCart.RestaurantId.HasValue && userCart.RestaurantId.HasValue && 
            guestCart.RestaurantId != userCart.RestaurantId)
        {
            _logger.LogInformation("Carts are from different restaurants. Guest: {GuestRestaurantId}, User: {UserRestaurantId}. Preferring most recent.", 
                guestCart.RestaurantId, userCart.RestaurantId);
            
            // Prefer the most recently updated cart
            if (guestCart.UpdatedAt > userCart.UpdatedAt)
            {
                _logger.LogInformation("Guest cart is more recent, replacing user cart");
                // Clear user cart and transfer guest cart items
                userCart.Items.Clear();
                foreach (var item in guestCart.Items)
                {
                    item.CartId = userCartId;
                    item.Cart = userCart;
                    userCart.Items.Add(item);
                }
                userCart.RestaurantId = guestCart.RestaurantId;
            }
            else
            {
                _logger.LogInformation("User cart is more recent, keeping user cart");
            }
        }
        else
        {
            // Same restaurant - merge items by MenuItemId
            _logger.LogInformation("Merging items from guest cart into user cart (same restaurant)");
            foreach (var guestItem in guestCart.Items)
            {
                var existingItem = userCart.Items.FirstOrDefault(i => i.MenuItemId == guestItem.MenuItemId);
                if (existingItem != null)
                {
                    // Merge quantities
                    existingItem.Quantity += guestItem.Quantity;
                    existingItem.TotalPrice = existingItem.Quantity * existingItem.UnitPrice;
                    _logger.LogDebug("Merged item {MenuItemId}: New quantity = {Quantity}", guestItem.MenuItemId, existingItem.Quantity);
                }
                else
                {
                    // Add new item to user cart
                    guestItem.CartId = userCartId;
                    guestItem.Cart = userCart;
                    userCart.Items.Add(guestItem);
                    _logger.LogDebug("Added new item {MenuItemId} to user cart", guestItem.MenuItemId);
                }
            }
        }

        RecalculateCartTotals(userCart);
        userCart.UpdatedAt = DateTime.UtcNow;

        // Delete guest cart items from database
        _context.CartItems.RemoveRange(guestCart.Items);
        _context.Carts.Remove(guestCart);

        await _context.SaveChangesAsync();

        // Update Redis cache
        await _redis.DeleteAsync($"cart:{guestCartId}");
        await _redis.SetAsync($"cart:{userCartId}", userCart, TimeSpan.FromHours(1));

        _logger.LogInformation("Successfully merged carts. Final cart ID: {UserCartId}, Item count: {ItemCount}", 
            userCartId, userCart.Items.Count);

        return userCartId;
    }

    public async Task<Guid> PlaceOrderAsync(Guid cartId, Guid customerId, string deliveryAddress, string idempotencyKey)
    {
        // Check idempotency
        var idempotencyKeyEntity = await _context.OrderIdempotencyKeys
            .FirstOrDefaultAsync(k => k.Key == idempotencyKey);

        if (idempotencyKeyEntity != null && idempotencyKeyEntity.OrderId.HasValue)
        {
            // Already processed
            return idempotencyKeyEntity.OrderId.Value;
        }

        // Check Redis for idempotency
        var redisKey = $"order:idem:{idempotencyKey}";
        if (await _redis.ExistsAsync(redisKey))
        {
            var existingOrderId = await _redis.GetAsync<Guid?>(redisKey);
            if (existingOrderId.HasValue)
                return existingOrderId.Value;
        }

        var cart = await GetCartAsync(cartId);
        if (cart == null || cart.Items.Count == 0)
            throw new InvalidOperationException("Cart is empty");

        var orderId = Guid.NewGuid();
        var order = new Order
        {
            OrderId = orderId,
            CustomerId = customerId,
            RestaurantId = cart.RestaurantId ?? throw new InvalidOperationException("Restaurant not set"),
            Subtotal = cart.Subtotal,
            Tax = cart.Tax,
            DeliveryFee = cart.DeliveryFee,
            Total = cart.Total,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            DeliveryAddress = deliveryAddress,
            IdempotencyKey = idempotencyKey,
            Items = cart.Items.Select(item => new OrderItem
            {
                OrderItemId = Guid.NewGuid(),
                OrderId = orderId,
                MenuItemId = item.MenuItemId,
                Name = item.Name,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.TotalPrice,
                ModifiersJson = item.SelectedOptionsJson
            }).ToList(),
            StatusHistory = new List<OrderStatusHistory>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    OrderId = orderId,
                    Status = "Pending",
                    ChangedAt = DateTime.UtcNow
                }
            }
        };

        _context.Orders.Add(order);

        // Store idempotency key
        var idempotencyEntity = new OrderIdempotencyKey
        {
            Id = Guid.NewGuid(),
            Key = idempotencyKey,
            OrderId = orderId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };
        _context.OrderIdempotencyKeys.Add(idempotencyEntity);

        await _context.SaveChangesAsync();

        // Cache idempotency
        await _redis.SetAsync(redisKey, orderId, TimeSpan.FromDays(1));

        // Publish event
        var orderPlacedEvent = new OrderPlacedEvent(
            orderId,
            customerId,
            cart.RestaurantId.Value,
            order.Total,
            order.CreatedAt,
            deliveryAddress,
            order.Items.Select(item => new OrderItemDto(
                item.MenuItemId,
                item.Name,
                item.Quantity,
                item.UnitPrice,
                item.TotalPrice,
                item.ModifiersJson != null 
                    ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(item.ModifiersJson) ?? new()
                    : new()
            )).ToList()
        );

        await _messagePublisher.PublishAsync("tradition-eats", "order.placed", orderPlacedEvent);

        return orderId;
    }

    public async Task<Order?> GetOrderAsync(Guid orderId)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.OrderId == orderId);
    }

    public async Task<List<Order>> GetOrdersByCustomerAsync(Guid customerId)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> UpdateOrderStatusAsync(Guid orderId, string newStatus, string? notes = null)
    {
        var order = await _context.Orders.FindAsync(orderId);
        if (order == null)
            return false;

        order.Status = newStatus;
        order.StatusHistory.Add(new OrderStatusHistory
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Status = newStatus,
            Notes = notes,
            ChangedAt = DateTime.UtcNow
        });

        if (newStatus == "Delivered")
        {
            order.DeliveredAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return true;
    }

    private void RecalculateCartTotals(Cart cart)
    {
        cart.Subtotal = cart.Items.Sum(i => i.TotalPrice);
        cart.Tax = cart.Subtotal * 0.08m; // 8% tax
        cart.DeliveryFee = 2.99m; // Fixed delivery fee
        cart.Total = cart.Subtotal + cart.Tax + cart.DeliveryFee;
    }
}
