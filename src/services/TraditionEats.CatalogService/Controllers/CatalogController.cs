using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TraditionEats.CatalogService.Services;

namespace TraditionEats.CatalogService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CatalogController : ControllerBase
{
    private readonly ICatalogService _catalogService;
    private readonly ILogger<CatalogController> _logger;

    public CatalogController(ICatalogService catalogService, ILogger<CatalogController> logger)
    {
        _catalogService = catalogService;
        _logger = logger;
    }

    // Categories
    [HttpPost("categories")]
    [Authorize(Roles = "Admin,RestaurantOwner")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryDto dto)
    {
        try
        {
            var categoryId = await _catalogService.CreateCategoryAsync(dto);
            return Ok(new { categoryId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create category");
            return StatusCode(500, new { message = "Failed to create category" });
        }
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        try
        {
            var categories = await _catalogService.GetCategoriesAsync();
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get categories");
            return StatusCode(500, new { message = "Failed to get categories" });
        }
    }

    [HttpGet("categories/{categoryId}")]
    public async Task<IActionResult> GetCategory(Guid categoryId)
    {
        try
        {
            var category = await _catalogService.GetCategoryAsync(categoryId);
            if (category == null)
            {
                return NotFound(new { message = "Category not found" });
            }
            return Ok(category);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get category");
            return StatusCode(500, new { message = "Failed to get category" });
        }
    }

    [HttpPut("categories/{categoryId}")]
    [Authorize(Roles = "Admin,RestaurantOwner")]
    public async Task<IActionResult> UpdateCategory(Guid categoryId, [FromBody] UpdateCategoryDto dto)
    {
        try
        {
            var success = await _catalogService.UpdateCategoryAsync(categoryId, dto);
            if (!success)
            {
                return NotFound(new { message = "Category not found" });
            }
            return Ok(new { message = "Category updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update category");
            return StatusCode(500, new { message = "Failed to update category" });
        }
    }

    // Menu Items
    [HttpPost("restaurants/{restaurantId}/menu-items")]
    [Authorize(Roles = "RestaurantOwner")]
    public async Task<IActionResult> CreateMenuItem(Guid restaurantId, [FromBody] CreateMenuItemDto dto)
    {
        try
        {
            var menuItemId = await _catalogService.CreateMenuItemAsync(restaurantId, dto);
            return Ok(new { menuItemId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create menu item");
            return StatusCode(500, new { message = "Failed to create menu item" });
        }
    }

    [HttpGet("menu-items/{menuItemId}")]
    public async Task<IActionResult> GetMenuItem(Guid menuItemId)
    {
        try
        {
            var menuItem = await _catalogService.GetMenuItemAsync(menuItemId);
            if (menuItem == null)
            {
                return NotFound(new { message = "Menu item not found" });
            }
            return Ok(menuItem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get menu item");
            return StatusCode(500, new { message = "Failed to get menu item" });
        }
    }

    [HttpGet("restaurants/{restaurantId}/menu-items")]
    public async Task<IActionResult> GetMenuItemsByRestaurant(
        Guid restaurantId,
        [FromQuery] Guid? categoryId = null)
    {
        try
        {
            var menuItems = await _catalogService.GetMenuItemsByRestaurantAsync(restaurantId, categoryId);
            return Ok(menuItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get menu items");
            return StatusCode(500, new { message = "Failed to get menu items" });
        }
    }

    [HttpPut("menu-items/{menuItemId}")]
    [Authorize(Roles = "RestaurantOwner")]
    public async Task<IActionResult> UpdateMenuItem(Guid menuItemId, [FromBody] UpdateMenuItemDto dto)
    {
        try
        {
            // Get restaurant ID from user claims or request
            // For now, we'll need to pass it or get it from the menu item
            var menuItem = await _catalogService.GetMenuItemAsync(menuItemId);
            if (menuItem == null)
            {
                return NotFound(new { message = "Menu item not found" });
            }

            var success = await _catalogService.UpdateMenuItemAsync(menuItemId, menuItem.RestaurantId, dto);
            if (!success)
            {
                return NotFound(new { message = "Menu item not found or you don't have permission" });
            }
            return Ok(new { message = "Menu item updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update menu item");
            return StatusCode(500, new { message = "Failed to update menu item" });
        }
    }

    [HttpPatch("menu-items/{menuItemId}/availability")]
    [Authorize(Roles = "RestaurantOwner")]
    public async Task<IActionResult> SetMenuItemAvailability(
        Guid menuItemId,
        [FromBody] SetAvailabilityRequest request)
    {
        try
        {
            var menuItem = await _catalogService.GetMenuItemAsync(menuItemId);
            if (menuItem == null)
            {
                return NotFound(new { message = "Menu item not found" });
            }

            var success = await _catalogService.SetMenuItemAvailabilityAsync(
                menuItemId, 
                menuItem.RestaurantId, 
                request.IsAvailable);
            
            if (!success)
            {
                return NotFound(new { message = "Menu item not found or you don't have permission" });
            }
            return Ok(new { message = "Menu item availability updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update menu item availability");
            return StatusCode(500, new { message = "Failed to update menu item availability" });
        }
    }

    // Menu Item Options
    [HttpPost("menu-items/{menuItemId}/options")]
    [Authorize(Roles = "RestaurantOwner")]
    public async Task<IActionResult> AddMenuItemOption(
        Guid menuItemId,
        [FromBody] CreateMenuItemOptionDto dto)
    {
        try
        {
            var menuItem = await _catalogService.GetMenuItemAsync(menuItemId);
            if (menuItem == null)
            {
                return NotFound(new { message = "Menu item not found" });
            }

            var optionId = await _catalogService.AddMenuItemOptionAsync(menuItemId, menuItem.RestaurantId, dto);
            return Ok(new { optionId });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add menu item option");
            return StatusCode(500, new { message = "Failed to add menu item option" });
        }
    }

    [HttpGet("menu-items/{menuItemId}/options")]
    public async Task<IActionResult> GetMenuItemOptions(Guid menuItemId)
    {
        try
        {
            var options = await _catalogService.GetMenuItemOptionsAsync(menuItemId);
            return Ok(options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get menu item options");
            return StatusCode(500, new { message = "Failed to get menu item options" });
        }
    }

    // Menu Item Prices
    [HttpPost("menu-items/{menuItemId}/prices")]
    [Authorize(Roles = "RestaurantOwner")]
    public async Task<IActionResult> AddMenuItemPrice(
        Guid menuItemId,
        [FromBody] CreateMenuItemPriceDto dto)
    {
        try
        {
            var menuItem = await _catalogService.GetMenuItemAsync(menuItemId);
            if (menuItem == null)
            {
                return NotFound(new { message = "Menu item not found" });
            }

            var priceId = await _catalogService.AddMenuItemPriceAsync(menuItemId, menuItem.RestaurantId, dto);
            return Ok(new { priceId });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add menu item price");
            return StatusCode(500, new { message = "Failed to add menu item price" });
        }
    }

    [HttpGet("menu-items/{menuItemId}/prices")]
    public async Task<IActionResult> GetMenuItemPrices(Guid menuItemId)
    {
        try
        {
            var prices = await _catalogService.GetMenuItemPricesAsync(menuItemId);
            return Ok(prices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get menu item prices");
            return StatusCode(500, new { message = "Failed to get menu item prices" });
        }
    }
}

public record SetAvailabilityRequest(bool IsAvailable);
