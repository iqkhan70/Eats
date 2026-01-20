using Microsoft.AspNetCore.Mvc;

namespace TraditionEats.Mobile.Bff.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HomeController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public HomeController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    [HttpGet("restaurants")]
    public async Task<IActionResult> GetRestaurants([FromQuery] double? latitude, [FromQuery] double? longitude)
    {
        var client = _httpClientFactory.CreateClient("RestaurantService");
        var response = await client.GetAsync($"/restaurant?latitude={latitude}&longitude={longitude}");
        
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            return Ok(content);
        }
        
        return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    [HttpGet("restaurants/{restaurantId}/menu")]
    public async Task<IActionResult> GetRestaurantMenu(Guid restaurantId)
    {
        var restaurantClient = _httpClientFactory.CreateClient("RestaurantService");
        var catalogClient = _httpClientFactory.CreateClient("CatalogService");

        var restaurantTask = restaurantClient.GetAsync($"/restaurant/{restaurantId}");
        var menuTask = catalogClient.GetAsync($"/catalog/restaurant/{restaurantId}/menu");

        await Task.WhenAll(restaurantTask, menuTask);

        var restaurantResponse = await restaurantTask.Result.Content.ReadAsStringAsync();
        var menuResponse = await menuTask.Result.Content.ReadAsStringAsync();

        return Ok(new { restaurant = restaurantResponse, menu = menuResponse });
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var client = _httpClientFactory.CreateClient("CatalogService");
        var response = await client.GetAsync("/catalog/categories");
        
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            return Ok(content);
        }
        
        return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }
}
