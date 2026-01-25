# Vendor Orders Management Implementation

## Status: In Progress

### Completed:
1. ✅ Added `GetOrdersByRestaurantAsync` method to `OrderService`
2. ✅ Created `OrderStatusChangedEvent` contract
3. ✅ Updated `UpdateOrderStatusAsync` to publish events
4. ✅ Added `UpdateOrderStatusRequest` record

### In Progress:
1. ⏳ Add vendor endpoints to `OrderController`:
   - `GET /api/Order/vendor/restaurants/{restaurantId}/orders` - Get orders for a restaurant
   - `PUT /api/Order/{orderId}/status` - Update order status

### Next Steps:
1. Integrate Vonage SMS service into NotificationService
2. Integrate Mailgun Email service into NotificationService
3. Create event handler in NotificationService to listen for "order.status.changed" events
4. Send email/SMS when status changes to "Ready"
5. Add vendor endpoints to Web BFF and Mobile BFF
6. Create Vendor Orders UI in WebApp
7. Create Vendor Orders UI in MobileApp
8. Ensure customer views show real-time status

## Manual Steps Required:

### Add to OrderController.cs (before closing brace at line 458):
```csharp
[HttpGet("vendor/restaurants/{restaurantId}/orders")]
[Authorize(Roles = "Vendor,Admin")]
public async Task<IActionResult> GetVendorOrders(Guid restaurantId)
{
    try
    {
        var orders = await _orderService.GetOrdersByRestaurantAsync(restaurantId);
        return Ok(orders);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to get vendor orders");
        return StatusCode(500, new { message = "Failed to get vendor orders" });
    }
}

[HttpPut("{orderId}/status")]
[Authorize(Roles = "Vendor,Admin")]
public async Task<IActionResult> UpdateOrderStatus(Guid orderId, [FromBody] UpdateOrderStatusRequest request)
{
    try
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Status))
        {
            return BadRequest(new { message = "Status is required" });
        }

        var order = await _orderService.GetOrderAsync(orderId);
        if (order == null)
        {
            return NotFound(new { message = "Order not found" });
        }

        var success = await _orderService.UpdateOrderStatusAsync(orderId, request.Status, request.Notes);
        if (!success)
        {
            return BadRequest(new { message = "Failed to update order status" });
        }

        return Ok(new { message = "Order status updated successfully" });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to update order status");
        return StatusCode(500, new { message = "Failed to update order status" });
    }
}
```
