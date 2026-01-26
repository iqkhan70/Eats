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
    Task<List<Order>> GetOrdersByRestaurantAsync(Guid restaurantId);
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
        _logger.LogInformation("CreateCartAsync: customerId={CustomerId}, restaurantId={RestaurantId}", customerId, restaurantId);

        var cartId = Guid.NewGuid();
        var cart = new Cart
        {
            CartId = cartId,
            CustomerId = customerId,
            RestaurantId = restaurantId,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Carts.Add(cart);

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("CreateCartAsync: Cart {CartId} saved to database", cartId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateCartAsync: Failed to save cart to database");
            throw;
        }

        // Cache cart (non-blocking)
        try
        {
            await _redis.SetAsync($"cart:{cartId}", cart, TimeSpan.FromHours(1));
            _logger.LogInformation("CreateCartAsync: Cart {CartId} cached in Redis", cartId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CreateCartAsync: Failed to cache cart in Redis, continuing anyway");
        }

        return cartId;
    }

    public async Task<Cart?> GetCartAsync(Guid cartId)
    {
        // Always invalidate cache first to ensure we get fresh data
        try
        {
            await _redis.DeleteAsync($"cart:{cartId}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetCartAsync: Failed to delete cart from Redis cache");
        }

        // Always load from database first to check for duplicates
        // (Don't trust cache if there might be duplicates)
        var trackedCart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CartId == cartId);

        if (trackedCart == null)
            return null;

        // Log what's actually in the database
        _logger.LogInformation("GetCartAsync: Loaded cart {CartId} from database - ItemCount={ItemCount}", cartId, trackedCart.Items?.Count ?? 0);
        if (trackedCart.Items != null)
        {
            foreach (var item in trackedCart.Items)
            {
                var expectedTotalPrice = item.Quantity * item.UnitPrice;
                _logger.LogInformation("GetCartAsync: DB Item - CartItemId={CartItemId}, MenuItemId={MenuItemId}, Name={Name}, Quantity={Quantity}, UnitPrice={UnitPrice}, TotalPrice={TotalPrice}, ExpectedTotalPrice={ExpectedTotalPrice}",
                    item.CartItemId, item.MenuItemId, item.Name, item.Quantity, item.UnitPrice, item.TotalPrice, expectedTotalPrice);
            }
        }

        // Validate and fix all items' TotalPrice
        bool needsSave = false;
        if (trackedCart.Items != null)
        {
            foreach (var item in trackedCart.Items)
            {
                var expectedTotalPrice = item.Quantity * item.UnitPrice;
                if (item.TotalPrice != expectedTotalPrice)
                {
                    _logger.LogWarning("GetCartAsync: Item {CartItemId} has incorrect TotalPrice! Expected={Expected}, Actual={Actual}. Fixing...",
                        item.CartItemId, expectedTotalPrice, item.TotalPrice);
                    item.TotalPrice = expectedTotalPrice;
                    needsSave = true;
                }
            }
        }

        // Clean up any duplicate items
        var hadDuplicates = await CleanupDuplicateItemsAsync(trackedCart);

        // Recalculate totals if we made changes
        if (needsSave || hadDuplicates)
        {
            RecalculateCartTotals(trackedCart);
            trackedCart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("GetCartAsync: Saved changes - ItemCount={ItemCount}, Subtotal={Subtotal}",
                trackedCart.Items?.Count ?? 0, trackedCart.Subtotal);
        }

        // Reload as NoTracking for return (to avoid tracking issues)
        var cart = await _context.Carts
            .Include(c => c.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CartId == cartId);

        if (cart != null)
        {
            // Final validation - ensure all TotalPrice values are correct
            foreach (var item in cart.Items)
            {
                var expectedTotalPrice = item.Quantity * item.UnitPrice;
                if (item.TotalPrice != expectedTotalPrice)
                {
                    _logger.LogError("GetCartAsync: Item {CartItemId} STILL has incorrect TotalPrice after reload! Expected={Expected}, Actual={Actual}. Fixing in memory...",
                        item.CartItemId, expectedTotalPrice, item.TotalPrice);
                    // Fix it in memory
                    item.TotalPrice = expectedTotalPrice;
                }
            }

            // Recalculate totals one final time
            RecalculateCartTotals(cart);

            // Cache the cleaned cart
            try
            {
                await _redis.SetAsync($"cart:{cartId}", cart, TimeSpan.FromHours(1));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GetCartAsync: Failed to cache cart in Redis");
            }
        }

        return cart;
    }

    private async Task<bool> CleanupDuplicateItemsAsync(Cart cart)
    {
        if (cart.Items == null || !cart.Items.Any())
            return false;

        // Log all items before cleanup
        _logger.LogInformation("CleanupDuplicateItemsAsync: Cart {CartId} has {ItemCount} items before cleanup", cart.CartId, cart.Items.Count);
        foreach (var item in cart.Items)
        {
            _logger.LogInformation("CleanupDuplicateItemsAsync: Item - CartItemId={CartItemId}, MenuItemId={MenuItemId}, Name={Name}, Quantity={Quantity}, UnitPrice={UnitPrice}, TotalPrice={TotalPrice}",
                item.CartItemId, item.MenuItemId, item.Name, item.Quantity, item.UnitPrice, item.TotalPrice);
        }

        // Check for duplicate items (same MenuItemId) - keep the one with the highest quantity
        var duplicateGroups = cart.Items
            .GroupBy(i => i.MenuItemId)
            .Where(g => g.Count() > 1)
            .ToList();

        if (!duplicateGroups.Any())
        {
            _logger.LogInformation("CleanupDuplicateItemsAsync: No duplicates found in cart {CartId}", cart.CartId);
            return false;
        }

        _logger.LogWarning("CleanupDuplicateItemsAsync: Found {DuplicateCount} duplicate groups in cart {CartId}", duplicateGroups.Count, cart.CartId);

        // Load cart with tracking to make changes
        var trackedCart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CartId == cart.CartId);

        if (trackedCart == null)
        {
            _logger.LogError("CleanupDuplicateItemsAsync: Cart {CartId} not found in database", cart.CartId);
            return false;
        }

        bool hasChanges = false;

        foreach (var duplicateGroup in duplicateGroups)
        {
            _logger.LogWarning("CleanupDuplicateItemsAsync: Found {Count} duplicate items for MenuItemId={MenuItemId}",
                duplicateGroup.Count(), duplicateGroup.Key);

            // Get all items with this MenuItemId from tracked cart
            var duplicateItems = trackedCart.Items
                .Where(i => i.MenuItemId == duplicateGroup.Key)
                .ToList();

            if (duplicateItems.Count <= 1)
                continue;

            // Keep the item with the highest quantity, remove others
            var itemsToKeep = duplicateItems
                .OrderByDescending(i => i.Quantity)
                .First();

            var itemsToRemove = duplicateItems
                .Where(i => i.CartItemId != itemsToKeep.CartItemId)
                .ToList();

            // Merge quantities into the kept item
            var totalQuantity = duplicateItems.Sum(i => i.Quantity);

            _logger.LogInformation("CleanupDuplicateItemsAsync: Before merge - Keeping CartItemId={CartItemId}, Quantity={Quantity}, TotalPrice={TotalPrice}",
                itemsToKeep.CartItemId, itemsToKeep.Quantity, itemsToKeep.TotalPrice);
            _logger.LogInformation("CleanupDuplicateItemsAsync: Removing {Count} duplicate items with total quantity {TotalQuantity}",
                itemsToRemove.Count, itemsToRemove.Sum(i => i.Quantity));

            itemsToKeep.Quantity = totalQuantity;
            itemsToKeep.TotalPrice = itemsToKeep.Quantity * itemsToKeep.UnitPrice;

            _logger.LogInformation("CleanupDuplicateItemsAsync: After merge - CartItemId={CartItemId}, Quantity={Quantity}, TotalPrice={TotalPrice}",
                itemsToKeep.CartItemId, itemsToKeep.Quantity, itemsToKeep.TotalPrice);

            // Remove duplicate items from database
            _context.CartItems.RemoveRange(itemsToRemove);
            // Remove from collection
            foreach (var item in itemsToRemove)
            {
                trackedCart.Items.Remove(item);
            }

            hasChanges = true;
        }

        if (hasChanges)
        {
            RecalculateCartTotals(trackedCart);
            trackedCart.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("CleanupDuplicateItemsAsync: Successfully cleaned up duplicates in cart {CartId}. New item count: {ItemCount}, New subtotal: {Subtotal}",
                cart.CartId, trackedCart.Items.Count, trackedCart.Subtotal);

            // Invalidate cache
            try
            {
                await _redis.DeleteAsync($"cart:{cart.CartId}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CleanupDuplicateItemsAsync: Failed to delete cart from Redis cache");
            }

            return true;
        }

        return false;
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
            // Validate and fix all items' TotalPrice before cleanup
            bool needsSave = false;
            var trackedCart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.CartId == cart.CartId);

            if (trackedCart != null && trackedCart.Items != null)
            {
                foreach (var item in trackedCart.Items)
                {
                    var expectedTotalPrice = item.Quantity * item.UnitPrice;
                    if (item.TotalPrice != expectedTotalPrice)
                    {
                        _logger.LogWarning("GetCartByCustomerAsync: Item {CartItemId} has incorrect TotalPrice! Expected={Expected}, Actual={Actual}. Fixing...",
                            item.CartItemId, expectedTotalPrice, item.TotalPrice);
                        item.TotalPrice = expectedTotalPrice;
                        needsSave = true;
                    }
                }

                if (needsSave)
                {
                    RecalculateCartTotals(trackedCart);
                    trackedCart.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("GetCartByCustomerAsync: Fixed incorrect TotalPrice values and saved");
                }
            }

            // Clean up any duplicate items before returning
            var hadDuplicates = await CleanupDuplicateItemsAsync(cart);

            // If duplicates were cleaned or prices were fixed, reload the cart to get the cleaned version
            if (hadDuplicates || needsSave)
            {
                // Reload with tracking to fix any remaining price issues
                var trackedCartForFix = await _context.Carts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.CartId == cart.CartId);

                if (trackedCartForFix != null)
                {
                    // Validate and fix all items' TotalPrice
                    bool needsFinalSave = false;
                    foreach (var item in trackedCartForFix.Items)
                    {
                        var expectedTotalPrice = item.Quantity * item.UnitPrice;
                        if (item.TotalPrice != expectedTotalPrice)
                        {
                            _logger.LogWarning("GetCartByCustomerAsync: Item {CartItemId} has incorrect TotalPrice after cleanup! Expected={Expected}, Actual={Actual}. Fixing...",
                                item.CartItemId, expectedTotalPrice, item.TotalPrice);
                            item.TotalPrice = expectedTotalPrice;
                            needsFinalSave = true;
                        }
                    }

                    if (needsFinalSave)
                    {
                        RecalculateCartTotals(trackedCartForFix);
                        trackedCartForFix.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("GetCartByCustomerAsync: Fixed TotalPrice values after cleanup");
                    }
                }

                // Now reload as NoTracking for return
                cart = await _context.Carts
                    .Include(c => c.Items)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CartId == cart.CartId);

                if (cart != null)
                {
                    // Final validation - ensure all TotalPrice values are correct in memory
                    foreach (var item in cart.Items)
                    {
                        var expectedTotalPrice = item.Quantity * item.UnitPrice;
                        if (item.TotalPrice != expectedTotalPrice)
                        {
                            _logger.LogError("GetCartByCustomerAsync: Item {CartItemId} STILL has incorrect TotalPrice after all fixes! Expected={Expected}, Actual={Actual}. Fixing in memory...",
                                item.CartItemId, expectedTotalPrice, item.TotalPrice);
                            // Fix it in memory for this response
                            item.TotalPrice = expectedTotalPrice;
                        }
                    }

                    // Recalculate totals on the cleaned cart
                    RecalculateCartTotals(cart);
                }
            }
            else
            {
                // Even if no cleanup was needed, validate all items one more time
                if (cart != null && cart.Items != null)
                {
                    foreach (var item in cart.Items)
                    {
                        var expectedTotalPrice = item.Quantity * item.UnitPrice;
                        if (item.TotalPrice != expectedTotalPrice)
                        {
                            _logger.LogWarning("GetCartByCustomerAsync: Item {CartItemId} has incorrect TotalPrice! Expected={Expected}, Actual={Actual}. Fixing in memory...",
                                item.CartItemId, expectedTotalPrice, item.TotalPrice);
                            // Fix it in memory for this response
                            item.TotalPrice = expectedTotalPrice;
                        }
                    }
                    RecalculateCartTotals(cart);
                }
            }

            if (cart != null)
            {
                await _redis.SetAsync($"cart:{cart.CartId}", cart, TimeSpan.FromHours(1));
            }
        }

        return cart;
    }

    public async Task AddItemToCartAsync(Guid cartId, Guid menuItemId, string name, decimal price, int quantity, Dictionary<string, string>? options)
    {
        _logger.LogInformation("AddItemToCartAsync: CartId={CartId}, MenuItemId={MenuItemId}, Quantity={Quantity}, Price={Price}",
            cartId, menuItemId, quantity, price);

        // Always load with tracking to ensure EF Core can detect changes
        var cart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CartId == cartId);

        if (cart == null)
        {
            _logger.LogError("AddItemToCartAsync: Cart {CartId} not found", cartId);
            throw new InvalidOperationException("Cart not found");
        }

        _logger.LogInformation("AddItemToCartAsync: Cart found with {ItemCount} items", cart.Items?.Count ?? 0);

        // Check for and remove duplicate items (same MenuItemId) - keep the one with the highest quantity
        bool hasDuplicates = false;
        if (cart.Items != null && cart.Items.Any())
        {
            var duplicateGroups = cart.Items
                .GroupBy(i => i.MenuItemId)
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var duplicateGroup in duplicateGroups)
            {
                hasDuplicates = true;
                _logger.LogWarning("AddItemToCartAsync: Found {Count} duplicate items for MenuItemId={MenuItemId}",
                    duplicateGroup.Count(), duplicateGroup.Key);

                // Keep the item with the highest quantity, remove others
                var itemsToKeep = duplicateGroup.OrderByDescending(i => i.Quantity).First();
                var itemsToRemove = duplicateGroup.Except(new[] { itemsToKeep }).ToList();

                // Merge quantities into the kept item
                var totalQuantity = duplicateGroup.Sum(i => i.Quantity);
                itemsToKeep.Quantity = totalQuantity;
                itemsToKeep.TotalPrice = itemsToKeep.Quantity * itemsToKeep.UnitPrice;

                _logger.LogInformation("AddItemToCartAsync: Merged duplicates - Keeping CartItemId={CartItemId}, MergedQuantity={Quantity}, Removing {RemoveCount} duplicates",
                    itemsToKeep.CartItemId, itemsToKeep.Quantity, itemsToRemove.Count);

                // Remove duplicate items from database
                _context.CartItems.RemoveRange(itemsToRemove);
                // Remove from collection
                foreach (var item in itemsToRemove)
                {
                    cart.Items.Remove(item);
                }
            }

            // Save duplicate removal before proceeding
            if (hasDuplicates)
            {
                RecalculateCartTotals(cart);
                cart.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation("AddItemToCartAsync: Removed duplicates and saved changes");
            }
        }

        // Log all existing items to help debug
        if (cart.Items != null && cart.Items.Any())
        {
            foreach (var existing in cart.Items)
            {
                _logger.LogInformation("AddItemToCartAsync: Existing cart item - CartItemId={CartItemId}, MenuItemId={MenuItemId}, Name={Name}, Quantity={Quantity}, UnitPrice={UnitPrice}, TotalPrice={TotalPrice}",
                    existing.CartItemId, existing.MenuItemId, existing.Name, existing.Quantity, existing.UnitPrice, existing.TotalPrice);
            }
        }

        // Check if item already exists in the tracked cart
        var existingItem = cart.Items?.FirstOrDefault(i => i.MenuItemId == menuItemId);

        if (existingItem != null)
        {
            // Item exists - update quantity
            _logger.LogInformation("AddItemToCartAsync: Updating existing item - CartItemId={CartItemId}, CurrentQuantity={CurrentQuantity}, AddingQuantity={AddingQuantity}, CurrentTotalPrice={CurrentTotalPrice}, UnitPrice={UnitPrice}",
                existingItem.CartItemId, existingItem.Quantity, quantity, existingItem.TotalPrice, existingItem.UnitPrice);

            var oldQuantity = existingItem.Quantity;
            var oldTotalPrice = existingItem.TotalPrice;
            existingItem.Quantity += quantity;

            // Ensure TotalPrice is correctly calculated
            existingItem.TotalPrice = existingItem.Quantity * existingItem.UnitPrice;

            _logger.LogInformation("AddItemToCartAsync: Updated item - CartItemId={CartItemId}, OldQuantity={OldQuantity}, OldTotalPrice={OldTotalPrice}, NewQuantity={NewQuantity}, NewTotalPrice={NewTotalPrice}, UnitPrice={UnitPrice}",
                existingItem.CartItemId, oldQuantity, oldTotalPrice, existingItem.Quantity, existingItem.TotalPrice, existingItem.UnitPrice);

            // Validate: TotalPrice should equal Quantity * UnitPrice
            var expectedTotalPrice = existingItem.Quantity * existingItem.UnitPrice;
            if (existingItem.TotalPrice != expectedTotalPrice)
            {
                _logger.LogError("AddItemToCartAsync: TotalPrice mismatch! Expected={Expected}, Actual={Actual}. Fixing...",
                    expectedTotalPrice, existingItem.TotalPrice);
                existingItem.TotalPrice = expectedTotalPrice;
            }
        }
        else
        {
            // Item doesn't exist - add new one
            _logger.LogInformation("AddItemToCartAsync: Adding new item - Quantity={Quantity}, UnitPrice={UnitPrice}", quantity, price);

            var expectedTotalPrice = price * quantity;
            var cartItem = new CartItem
            {
                CartItemId = Guid.NewGuid(),
                CartId = cartId,
                MenuItemId = menuItemId,
                Name = name,
                Quantity = quantity,
                UnitPrice = price,
                TotalPrice = expectedTotalPrice,
                SelectedOptionsJson = options != null ? System.Text.Json.JsonSerializer.Serialize(options) : null
            };

            // Validate TotalPrice
            if (cartItem.TotalPrice != expectedTotalPrice)
            {
                _logger.LogError("AddItemToCartAsync: TotalPrice mismatch on creation! Expected={Expected}, Actual={Actual}. Fixing...",
                    expectedTotalPrice, cartItem.TotalPrice);
                cartItem.TotalPrice = expectedTotalPrice;
            }

            _logger.LogInformation("AddItemToCartAsync: Created cart item - CartItemId={CartItemId}, MenuItemId={MenuItemId}, Quantity={Quantity}, UnitPrice={UnitPrice}, TotalPrice={TotalPrice}",
                cartItem.CartItemId, cartItem.MenuItemId, cartItem.Quantity, cartItem.UnitPrice, cartItem.TotalPrice);

            _context.CartItems.Add(cartItem);
            cart.Items.Add(cartItem);
        }

        var subtotalBefore = cart.Subtotal;
        RecalculateCartTotals(cart);
        cart.UpdatedAt = DateTime.UtcNow;

        _logger.LogInformation("AddItemToCartAsync: Cart totals - SubtotalBefore={SubtotalBefore}, SubtotalAfter={SubtotalAfter}, Total={Total}, ItemCount={ItemCount}",
            subtotalBefore, cart.Subtotal, cart.Total, cart.Items.Count);

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("AddItemToCartAsync: Changes saved to database");
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException?.Message?.Contains("Duplicate entry") == true ||
                                                                          ex.InnerException?.Message?.Contains("UNIQUE constraint") == true ||
                                                                          ex.InnerException?.Message?.Contains("Duplicate key") == true)
        {
            // Handle unique constraint violation - item was added concurrently
            _logger.LogWarning(ex, "AddItemToCartAsync: Duplicate item detected (concurrent add), reloading cart and updating quantity");

            // Reload cart and try to update existing item
            cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.CartId == cartId);

            if (cart != null)
            {
                var duplicateItem = cart.Items?.FirstOrDefault(i => i.MenuItemId == menuItemId);
                if (duplicateItem != null)
                {
                    duplicateItem.Quantity += quantity;
                    duplicateItem.TotalPrice = duplicateItem.Quantity * duplicateItem.UnitPrice;
                    RecalculateCartTotals(cart);
                    cart.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    _logger.LogInformation("AddItemToCartAsync: Successfully updated duplicate item - Quantity={Quantity}, TotalPrice={TotalPrice}",
                        duplicateItem.Quantity, duplicateItem.TotalPrice);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AddItemToCartAsync: Failed to save changes to database - {Message}", ex.Message);
            throw;
        }

        // Invalidate cache and reload cart to get latest state (non-blocking)
        try
        {
            await _redis.DeleteAsync($"cart:{cartId}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AddItemToCartAsync: Failed to delete cart from Redis cache, continuing anyway");
        }

        // Reload cart from database to get the latest state for caching
        try
        {
            cart = await _context.Carts
                .Include(c => c.Items)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CartId == cartId);

            if (cart != null)
            {
                _logger.LogInformation("AddItemToCartAsync: Reloaded cart - FinalItemCount={ItemCount}, FinalSubtotal={Subtotal}, FinalTotal={Total}",
                    cart.Items.Count, cart.Subtotal, cart.Total);

                try
                {
                    await _redis.SetAsync($"cart:{cartId}", cart, TimeSpan.FromHours(1));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "AddItemToCartAsync: Failed to cache cart in Redis, continuing anyway");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AddItemToCartAsync: Failed to reload cart for caching, continuing anyway");
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
        _logger.LogInformation("RemoveCartItemAsync: CartId={CartId}, CartItemId={CartItemId}", cartId, cartItemId);

        // Always load with tracking to ensure EF Core can detect changes
        var cart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CartId == cartId);

        if (cart == null)
        {
            _logger.LogError("RemoveCartItemAsync: Cart {CartId} not found", cartId);
            throw new InvalidOperationException($"Cart {cartId} not found");
        }

        _logger.LogInformation("RemoveCartItemAsync: Cart found with {ItemCount} items", cart.Items?.Count ?? 0);

        var item = cart.Items.FirstOrDefault(i => i.CartItemId == cartItemId);
        if (item == null)
        {
            _logger.LogWarning("RemoveCartItemAsync: Cart item {CartItemId} not found in cart {CartId}", cartItemId, cartId);
            throw new InvalidOperationException($"Cart item {cartItemId} not found");
        }

        _logger.LogInformation("RemoveCartItemAsync: Removing item - Name={Name}, Quantity={Quantity}, TotalPrice={TotalPrice}",
            item.Name, item.Quantity, item.TotalPrice);

        cart.Items.Remove(item);
        _context.CartItems.Remove(item);

        RecalculateCartTotals(cart);
        cart.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("RemoveCartItemAsync: Item removed successfully. New item count: {ItemCount}, New total: {Total}",
                cart.Items.Count, cart.Total);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RemoveCartItemAsync: Failed to save changes to database - {Message}", ex.Message);
            throw;
        }

        // Invalidate cache and reload cart
        try
        {
            await _redis.DeleteAsync($"cart:{cartId}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RemoveCartItemAsync: Failed to delete cart from Redis cache, continuing anyway");
        }

        // Reload cart from database to get the latest state for caching
        try
        {
            cart = await _context.Carts
                .Include(c => c.Items)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CartId == cartId);

            if (cart != null)
            {
                try
                {
                    await _redis.SetAsync($"cart:{cartId}", cart, TimeSpan.FromHours(1));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "RemoveCartItemAsync: Failed to cache cart in Redis, continuing anyway");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RemoveCartItemAsync: Failed to reload cart for caching, continuing anyway");
        }
    }

    public async Task ClearCartAsync(Guid cartId)
    {
        _logger.LogInformation("ClearCartAsync: CartId={CartId}", cartId);

        // Always load with tracking to ensure EF Core can detect changes
        var cart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CartId == cartId);

        if (cart == null)
        {
            _logger.LogWarning("ClearCartAsync: Cart {CartId} not found - may have already been deleted or never existed", cartId);
            // Cart doesn't exist - this is fine, it's already "cleared"
            // Just clear any cached data and return
            try
            {
                await _redis.DeleteAsync($"cart:{cartId}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ClearCartAsync: Failed to delete cart from Redis cache");
            }
            return; // Success - cart is already cleared (doesn't exist)
        }

        _logger.LogInformation("ClearCartAsync: Cart found with {ItemCount} items. Clearing all items.", cart.Items?.Count ?? 0);

        // Remove all items from database
        if (cart.Items != null && cart.Items.Any())
        {
            _context.CartItems.RemoveRange(cart.Items);
        }

        cart.Items.Clear();
        RecalculateCartTotals(cart);
        cart.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("ClearCartAsync: Cart cleared successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClearCartAsync: Failed to save changes to database - {Message}", ex.Message);
            throw;
        }

        // Invalidate cache and reload cart
        try
        {
            await _redis.DeleteAsync($"cart:{cartId}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ClearCartAsync: Failed to delete cart from Redis cache, continuing anyway");
        }

        // Reload cart from database to get the latest state for caching
        try
        {
            cart = await _context.Carts
                .Include(c => c.Items)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CartId == cartId);

            if (cart != null)
            {
                try
                {
                    await _redis.SetAsync($"cart:{cartId}", cart, TimeSpan.FromHours(1));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "ClearCartAsync: Failed to cache cart in Redis, continuing anyway");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ClearCartAsync: Failed to reload cart for caching, continuing anyway");
        }
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
        _logger.LogInformation("PlaceOrderAsync: Starting - CartId={CartId}, CustomerId={CustomerId}", cartId, customerId);

        // Check idempotency
        var idempotencyKeyEntity = await _context.OrderIdempotencyKeys
            .FirstOrDefaultAsync(k => k.Key == idempotencyKey);

        if (idempotencyKeyEntity != null && idempotencyKeyEntity.OrderId.HasValue)
        {
            _logger.LogInformation("PlaceOrderAsync: Order already exists for idempotency key {IdempotencyKey}, returning existing OrderId={OrderId}",
                idempotencyKey, idempotencyKeyEntity.OrderId.Value);
            return idempotencyKeyEntity.OrderId.Value;
        }

        // Check Redis for idempotency
        var redisKey = $"order:idem:{idempotencyKey}";
        if (await _redis.ExistsAsync(redisKey))
        {
            var existingOrderId = await _redis.GetAsync<Guid?>(redisKey);
            if (existingOrderId.HasValue)
            {
                _logger.LogInformation("PlaceOrderAsync: Order already exists in Redis for idempotency key {IdempotencyKey}, returning existing OrderId={OrderId}",
                    idempotencyKey, existingOrderId.Value);
                return existingOrderId.Value;
            }
        }

        // Load cart directly from database (not cache) to ensure we have the latest data
        // This prevents using stale cached data that might have doubled items
        var cart = await _context.Carts
            .Include(c => c.Items)
            .AsNoTracking() // Use AsNoTracking since we're only reading
            .FirstOrDefaultAsync(c => c.CartId == cartId);

        if (cart == null)
        {
            _logger.LogError("PlaceOrderAsync: Cart {CartId} not found", cartId);
            throw new InvalidOperationException($"Cart {cartId} not found");
        }

        if (cart.Items == null || cart.Items.Count == 0)
        {
            _logger.LogError("PlaceOrderAsync: Cart {CartId} is empty", cartId);
            throw new InvalidOperationException("Cart is empty");
        }

        if (!cart.RestaurantId.HasValue)
        {
            _logger.LogError("PlaceOrderAsync: Cart {CartId} has no restaurant ID", cartId);
            throw new InvalidOperationException("Restaurant not set in cart");
        }

        _logger.LogInformation("PlaceOrderAsync: Cart found - CartId={CartId}, RestaurantId={RestaurantId}, ItemCount={ItemCount}, Subtotal={Subtotal}, Tax={Tax}, DeliveryFee={DeliveryFee}, Total={Total}",
            cartId, cart.RestaurantId.Value, cart.Items.Count, cart.Subtotal, cart.Tax, cart.DeliveryFee, cart.Total);

        // Log each item to help debug price doubling
        foreach (var item in cart.Items)
        {
            _logger.LogInformation("PlaceOrderAsync: Cart item - MenuItemId={MenuItemId}, Name={Name}, Quantity={Quantity}, UnitPrice={UnitPrice}, TotalPrice={TotalPrice}",
                item.MenuItemId, item.Name, item.Quantity, item.UnitPrice, item.TotalPrice);
        }

        var orderId = Guid.NewGuid();

        // Calculate totals from items to ensure accuracy (don't trust cart totals if items are doubled)
        var orderItems = cart.Items.Select(item => new OrderItem
        {
            OrderItemId = Guid.NewGuid(),
            OrderId = orderId,
            MenuItemId = item.MenuItemId,
            Name = item.Name,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            TotalPrice = item.TotalPrice,
            ModifiersJson = item.SelectedOptionsJson
        }).ToList();

        // Recalculate totals from order items to ensure accuracy
        var orderSubtotal = orderItems.Sum(i => i.TotalPrice);
        var orderTax = orderSubtotal * 0.08m; // 8% tax
        var orderDeliveryFee = 2.99m; // Fixed delivery fee
        var orderTotal = orderSubtotal + orderTax + orderDeliveryFee;

        _logger.LogInformation("PlaceOrderAsync: Calculated order totals - Subtotal={Subtotal}, Tax={Tax}, DeliveryFee={DeliveryFee}, Total={Total}",
            orderSubtotal, orderTax, orderDeliveryFee, orderTotal);
        _logger.LogInformation("PlaceOrderAsync: Cart totals - Subtotal={Subtotal}, Tax={Tax}, DeliveryFee={DeliveryFee}, Total={Total}",
            cart.Subtotal, cart.Tax, cart.DeliveryFee, cart.Total);

        var order = new Order
        {
            OrderId = orderId,
            CustomerId = customerId,
            RestaurantId = cart.RestaurantId.Value,
            Subtotal = orderSubtotal, // Use recalculated subtotal
            Tax = orderTax, // Use recalculated tax
            DeliveryFee = orderDeliveryFee, // Use recalculated delivery fee
            Total = orderTotal, // Use recalculated total
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            DeliveryAddress = deliveryAddress,
            IdempotencyKey = idempotencyKey,
            Items = orderItems,
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

        _logger.LogInformation("PlaceOrderAsync: Creating order - OrderId={OrderId}, CustomerId={CustomerId}, RestaurantId={RestaurantId}, ItemCount={ItemCount}, Total={Total}",
            orderId, customerId, cart.RestaurantId.Value, orderItems.Count, orderTotal);

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

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("PlaceOrderAsync: Order {OrderId} saved to database", orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PlaceOrderAsync: Failed to save order to database");
            throw;
        }

        // Cache idempotency
        try
        {
            await _redis.SetAsync(redisKey, orderId, TimeSpan.FromDays(1));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PlaceOrderAsync: Failed to cache idempotency key in Redis, continuing anyway");
        }

        // Publish event
        try
        {
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
            _logger.LogInformation("PlaceOrderAsync: Order placed event published for OrderId={OrderId}", orderId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PlaceOrderAsync: Failed to publish order placed event, continuing anyway");
            // Don't fail the order if event publishing fails
        }

        // Clear the cart after successful order placement
        try
        {
            _logger.LogInformation("PlaceOrderAsync: Clearing cart {CartId} after order placement", cartId);

            // Load cart with tracking to clear items
            var cartToClear = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.CartId == cartId);

            if (cartToClear != null)
            {
                // Remove all items from database
                if (cartToClear.Items != null && cartToClear.Items.Any())
                {
                    _context.CartItems.RemoveRange(cartToClear.Items);
                }

                cartToClear.Items.Clear();
                RecalculateCartTotals(cartToClear);
                cartToClear.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                _logger.LogInformation("PlaceOrderAsync: Cart {CartId} cleared successfully", cartId);

                // Invalidate Redis cache
                try
                {
                    await _redis.DeleteAsync($"cart:{cartId}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "PlaceOrderAsync: Failed to delete cart from Redis cache, continuing anyway");
                }
            }
            else
            {
                _logger.LogWarning("PlaceOrderAsync: Cart {CartId} not found for clearing (may have already been cleared)", cartId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PlaceOrderAsync: Failed to clear cart {CartId} after order placement, continuing anyway - {Message}",
                cartId, ex.Message);
            // Don't fail the order if cart clearing fails - order is already placed
        }

        _logger.LogInformation("PlaceOrderAsync: Successfully placed order {OrderId}", orderId);
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
        _logger.LogInformation("GetOrdersByCustomerAsync: CustomerId={CustomerId}", customerId);

        var orders = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        _logger.LogInformation("GetOrdersByCustomerAsync: Found {OrderCount} orders for CustomerId={CustomerId}", orders.Count, customerId);

        foreach (var order in orders)
        {
            _logger.LogInformation("GetOrdersByCustomerAsync: Order - OrderId={OrderId}, CustomerId={CustomerId}, Status={Status}, Total={Total}, CreatedAt={CreatedAt}",
                order.OrderId, order.CustomerId, order.Status, order.Total, order.CreatedAt);
        }

        return orders;
    }

    public async Task<List<Order>> GetOrdersByRestaurantAsync(Guid restaurantId)
    {
        _logger.LogInformation("GetOrdersByRestaurantAsync: RestaurantId={RestaurantId}", restaurantId);

        var orders = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .Where(o => o.RestaurantId == restaurantId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        _logger.LogInformation("GetOrdersByRestaurantAsync: Found {OrderCount} orders for RestaurantId={RestaurantId}", orders.Count, restaurantId);

        return orders;
    }

    public async Task<bool> UpdateOrderStatusAsync(Guid orderId, string newStatus, string? notes = null)
    {
        Order? order = null;
        string? oldStatus = null;

        try
        {
            _logger.LogInformation("UpdateOrderStatusAsync: Starting - OrderId={OrderId}, NewStatus={NewStatus}, Notes={Notes}",
                orderId, newStatus, notes ?? "null");

            // Load order to get current status (no tracking to avoid concurrency issues)
            order = await _context.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
            {
                _logger.LogWarning("UpdateOrderStatusAsync: Order not found - OrderId={OrderId}", orderId);
                return false;
            }

            oldStatus = order.Status;
            _logger.LogInformation("UpdateOrderStatusAsync: Order found - OrderId={OrderId}, CurrentStatus={CurrentStatus}, CustomerId={CustomerId}, RestaurantId={RestaurantId}",
                orderId, oldStatus, order.CustomerId, order.RestaurantId);

            // Use ExecuteUpdateAsync for direct database update (avoids concurrency issues)
            var rowsAffected = await _context.Orders
                .Where(o => o.OrderId == orderId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(o => o.Status, newStatus));

            // Only update DeliveredAt if status is "Delivered"
            if (newStatus == "Delivered" && rowsAffected > 0)
            {
                await _context.Orders
                    .Where(o => o.OrderId == orderId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(o => o.DeliveredAt, DateTime.UtcNow));
            }

            if (rowsAffected == 0)
            {
                _logger.LogWarning("UpdateOrderStatusAsync: No rows affected - OrderId={OrderId} may have been deleted", orderId);
                return false;
            }

            _logger.LogInformation("UpdateOrderStatusAsync: Order status updated - RowsAffected={RowsAffected}", rowsAffected);

            // Create and add new status history entry directly to context
            var statusHistory = new OrderStatusHistory
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                Status = newStatus,
                Notes = notes,
                ChangedAt = DateTime.UtcNow
            };

            // Add to context directly
            await _context.OrderStatusHistory.AddAsync(statusHistory);

            _logger.LogInformation("UpdateOrderStatusAsync: Saving status history to database");
            await _context.SaveChangesAsync();
            _logger.LogInformation("UpdateOrderStatusAsync: Database changes saved successfully");

            // Publish order status changed event for notification service
            try
            {
                if (order != null && !string.IsNullOrEmpty(oldStatus))
                {
                    var statusChangedEvent = new OrderStatusChangedEvent(
                        orderId,
                        order.CustomerId,
                        order.RestaurantId,
                        oldStatus,
                        newStatus,
                        notes,
                        DateTime.UtcNow
                    );

                    await _messagePublisher.PublishAsync("tradition-eats", "order.status.changed", statusChangedEvent);
                    _logger.LogInformation("UpdateOrderStatusAsync: Order status changed event published - OrderId={OrderId}, OldStatus={OldStatus}, NewStatus={NewStatus}",
                        orderId, oldStatus, newStatus);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "UpdateOrderStatusAsync: Failed to publish order status changed event, continuing anyway");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateOrderStatusAsync: Exception occurred - OrderId={OrderId}, Exception={Exception}",
                orderId, ex.ToString());
            throw; // Re-throw to let controller handle it
        }
    }

    private void RecalculateCartTotals(Cart cart)
    {
        if (cart.Items == null || !cart.Items.Any())
        {
            cart.Subtotal = 0;
            cart.Tax = 0;
            cart.DeliveryFee = 0;
            cart.Total = 0;
            return;
        }

        // Log each item's contribution to subtotal
        _logger.LogInformation("RecalculateCartTotals: Calculating totals for cart {CartId} with {ItemCount} items", cart.CartId, cart.Items.Count);
        decimal subtotal = 0;
        foreach (var item in cart.Items)
        {
            _logger.LogInformation("RecalculateCartTotals: Item - CartItemId={CartItemId}, MenuItemId={MenuItemId}, Name={Name}, Quantity={Quantity}, UnitPrice={UnitPrice}, TotalPrice={TotalPrice}",
                item.CartItemId, item.MenuItemId, item.Name, item.Quantity, item.UnitPrice, item.TotalPrice);
            subtotal += item.TotalPrice;
        }

        cart.Subtotal = subtotal;
        cart.Tax = cart.Subtotal * 0.08m; // 8% tax
        cart.DeliveryFee = 2.99m; // Fixed delivery fee
        cart.Total = cart.Subtotal + cart.Tax + cart.DeliveryFee;

        _logger.LogInformation("RecalculateCartTotals: Final totals - Subtotal={Subtotal}, Tax={Tax}, DeliveryFee={DeliveryFee}, Total={Total}",
            cart.Subtotal, cart.Tax, cart.DeliveryFee, cart.Total);
    }
}
