using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TraditionEats.NotificationService.Services;

namespace TraditionEats.NotificationService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationController> _logger;

    public NotificationController(INotificationService notificationService, ILogger<NotificationController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    [HttpPost("templates")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateNotificationTemplateDto dto)
    {
        try
        {
            var templateId = await _notificationService.CreateTemplateAsync(dto);
            return Ok(new { templateId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create notification template");
            return StatusCode(500, new { message = "Failed to create notification template" });
        }
    }

    [HttpGet("templates")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetTemplates([FromQuery] string? type = null)
    {
        try
        {
            var templates = await _notificationService.GetTemplatesAsync(type);
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get templates");
            return StatusCode(500, new { message = "Failed to get templates" });
        }
    }

    [HttpGet("templates/{templateId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetTemplate(Guid templateId)
    {
        try
        {
            var template = await _notificationService.GetTemplateAsync(templateId);
            if (template == null)
            {
                return NotFound(new { message = "Template not found" });
            }
            return Ok(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get template");
            return StatusCode(500, new { message = "Failed to get template" });
        }
    }

    [HttpPost("send")]
    [Authorize]
    public async Task<IActionResult> SendNotification([FromBody] SendNotificationDto dto)
    {
        try
        {
            // If UserId not provided, use current user
            if (!dto.UserId.HasValue)
            {
                var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                dto = dto with { UserId = userId };
            }

            var success = await _notificationService.SendNotificationAsync(dto);
            return Ok(new { success, message = success ? "Notification sent successfully" : "Failed to send notification" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification");
            return StatusCode(500, new { message = "Failed to send notification" });
        }
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMyNotifications(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var notifications = await _notificationService.GetUserNotificationsAsync(userId, skip, take);
            return Ok(notifications);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get notifications");
            return StatusCode(500, new { message = "Failed to get notifications" });
        }
    }

    [HttpPost("preferences")]
    [Authorize]
    public async Task<IActionResult> SetNotificationPreference([FromBody] SetNotificationPreferenceDto dto)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var success = await _notificationService.SetNotificationPreferenceAsync(userId, dto);
            return Ok(new { success, message = "Preference updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set notification preference");
            return StatusCode(500, new { message = "Failed to set notification preference" });
        }
    }

    [HttpGet("preferences")]
    [Authorize]
    public async Task<IActionResult> GetNotificationPreferences()
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var preferences = await _notificationService.GetNotificationPreferencesAsync(userId);
            return Ok(preferences);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get notification preferences");
            return StatusCode(500, new { message = "Failed to get notification preferences" });
        }
    }
}
