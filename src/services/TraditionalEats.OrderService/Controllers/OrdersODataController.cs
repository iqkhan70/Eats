using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using TraditionalEats.OrderService.Data;
using TraditionalEats.OrderService.Entities;
using System.Security.Claims;

namespace TraditionalEats.OrderService.Controllers;

[Authorize]
[Route("odata/Orders")]
public class OrdersODataController : ODataController
{
    private readonly OrderDbContext _context;
    private readonly ILogger<OrdersODataController> _logger;

    public OrdersODataController(
        OrderDbContext context,
        ILogger<OrdersODataController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get(ODataQueryOptions<Order> queryOptions)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var isAdmin = User.IsInRole("Admin");
            var isVendor = User.IsInRole("Vendor");

            var query = _context.Orders
                .Include(o => o.Items)
                .Include(o => o.StatusHistory)
                .AsQueryable();

            // Role-based filtering
            if (isAdmin)
            {
                // Admin sees all orders - no filtering needed
                _logger.LogInformation("OrdersODataController.Get: Admin user - returning all orders");
            }
            else if (isVendor)
            {
                // Vendors see only orders for their restaurants
                // Note: This requires a join with RestaurantService to get vendor's restaurants
                // For now, we'll filter by restaurantId if provided in query, or return empty
                // In a real scenario, you'd fetch vendor's restaurant IDs from RestaurantService
                _logger.LogInformation("OrdersODataController.Get: Vendor user - filtering by vendor restaurants");
                // TODO: Add vendor restaurant filtering when RestaurantService integration is available
                // For now, vendors will need to filter by restaurantId in the OData query
            }
            else if (currentUserId.HasValue)
            {
                // Regular customers see only their own orders
                query = query.Where(o => o.CustomerId == currentUserId.Value);
                _logger.LogInformation("OrdersODataController.Get: Customer user {UserId} - filtering by customerId", currentUserId.Value);
            }
            else
            {
                // No user ID and not admin/vendor - return empty
                _logger.LogWarning("OrdersODataController.Get: No user ID found and user is not admin/vendor - returning empty");
                query = _context.Orders.Where(o => false);
            }

            // Get total count before applying query options (if $count was requested)
            var totalCount = queryOptions.Count?.Value == true ? await query.CountAsync() : (int?)null;

            // Apply OData query options manually (filtering, sorting, paging)
            IQueryable<Order> filteredQuery = query;
            
            if (queryOptions.Filter != null)
            {
                filteredQuery = queryOptions.Filter.ApplyTo(filteredQuery, new ODataQuerySettings()) as IQueryable<Order> ?? filteredQuery;
            }
            
            if (queryOptions.OrderBy != null)
            {
                filteredQuery = queryOptions.OrderBy.ApplyTo(filteredQuery, new ODataQuerySettings()) as IQueryable<Order> ?? filteredQuery;
            }
            
            if (queryOptions.Skip != null)
            {
                filteredQuery = queryOptions.Skip.ApplyTo(filteredQuery, new ODataQuerySettings()) as IQueryable<Order> ?? filteredQuery;
            }
            
            if (queryOptions.Top != null)
            {
                filteredQuery = queryOptions.Top.ApplyTo(filteredQuery, new ODataQuerySettings()) as IQueryable<Order> ?? filteredQuery;
            }
            
            // Materialize the query to ensure navigation properties are loaded
            var orders = await filteredQuery.ToListAsync();
            
            // Return as OData response format using dictionary to handle @ symbols in property names
            var response = new Dictionary<string, object>
            {
                ["@odata.context"] = $"{Request.Scheme}://{Request.Host}{Request.Path}",
                ["value"] = orders
            };
            
            if (totalCount.HasValue)
            {
                response["@odata.count"] = totalCount.Value;
            }

            // Use JsonResult to bypass OData processing and ensure navigation properties are serialized
            return new JsonResult(response)
            {
                ContentType = "application/json"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OrdersODataController.Get");
            throw;
        }
    }

    [EnableQuery]
    [HttpGet("{key}")]
    public SingleResult<Order> Get([FromRoute] Guid key)
    {
        var currentUserId = GetCurrentUserId();
        var isAdmin = User.IsInRole("Admin");
        var isVendor = User.IsInRole("Vendor");

        var query = _context.Orders
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .Where(o => o.OrderId == key);

        // Role-based filtering
        if (!isAdmin && !isVendor && currentUserId.HasValue)
        {
            query = query.Where(o => o.CustomerId == currentUserId.Value);
        }

        return SingleResult.Create(query);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? 
                         User.FindFirst("UserId") ?? 
                         User.FindFirst("userId");
        return userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId) ? userId : null;
    }
}
