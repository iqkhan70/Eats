using Microsoft.EntityFrameworkCore;
using TraditionalEats.NotificationService.Data;
using TraditionalEats.NotificationService.Entities;

namespace TraditionalEats.NotificationService.Services;

public interface INotificationService
{
    Task<Guid> CreateTemplateAsync(CreateNotificationTemplateDto dto);
    Task<NotificationTemplateDto?> GetTemplateAsync(Guid templateId);
    Task<List<NotificationTemplateDto>> GetTemplatesAsync(string? type = null);
    Task<bool> SendNotificationAsync(SendNotificationDto dto);
    Task<List<NotificationDto>> GetUserNotificationsAsync(Guid userId, int skip = 0, int take = 20);
    Task<bool> SetNotificationPreferenceAsync(Guid userId, SetNotificationPreferenceDto dto);
    Task<List<NotificationPreferenceDto>> GetNotificationPreferencesAsync(Guid userId);
}

public class NotificationService : INotificationService
{
    private readonly NotificationDbContext _context;
    private readonly ILogger<NotificationService> _logger;
    private readonly IConfiguration _configuration;

    public NotificationService(
        NotificationDbContext context,
        ILogger<NotificationService> logger,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<Guid> CreateTemplateAsync(CreateNotificationTemplateDto dto)
    {
        var templateId = Guid.NewGuid();

        var template = new NotificationTemplate
        {
            TemplateId = templateId,
            Name = dto.Name,
            Type = dto.Type,
            Subject = dto.Subject,
            Body = dto.Body,
            VariablesJson = System.Text.Json.JsonSerializer.Serialize(dto.Variables ?? new List<string>()),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Templates.Add(template);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created notification template {TemplateId}: {Name}", templateId, dto.Name);
        return templateId;
    }

    public async Task<NotificationTemplateDto?> GetTemplateAsync(Guid templateId)
    {
        var template = await _context.Templates
            .FirstOrDefaultAsync(t => t.TemplateId == templateId);

        return template == null ? null : MapToDto(template);
    }

    public async Task<List<NotificationTemplateDto>> GetTemplatesAsync(string? type = null)
    {
        var query = _context.Templates
            .Where(t => t.IsActive)
            .AsQueryable();

        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(t => t.Type == type);
        }

        var templates = await query.ToListAsync();
        return templates.Select(MapToDto).ToList();
    }

    public async Task<bool> SendNotificationAsync(SendNotificationDto dto)
    {
        // Check user preferences
        var preference = await _context.Preferences
            .FirstOrDefaultAsync(p => p.UserId == dto.UserId 
                && p.Channel == dto.Channel 
                && p.NotificationType == dto.Type);

        if (preference != null && !preference.IsEnabled)
        {
            _logger.LogInformation("Notification disabled by user preference for user {UserId}, channel {Channel}, type {Type}", 
                dto.UserId, dto.Channel, dto.Type);
            return false;
        }

        var notificationId = Guid.NewGuid();
        var notification = new Notification
        {
            NotificationId = notificationId,
            UserId = dto.UserId,
            Channel = dto.Channel,
            Type = dto.Type,
            TemplateId = dto.TemplateId?.ToString(),
            Subject = dto.Subject,
            Body = dto.Body,
            Recipient = dto.Recipient,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Notifications.Add(notification);

        try
        {
            // Send notification based on channel
            bool sent = false;
            switch (dto.Channel.ToLower())
            {
                case "email":
                    sent = await SendEmailAsync(dto.Recipient!, dto.Subject, dto.Body);
                    break;
                case "sms":
                    sent = await SendSmsAsync(dto.Recipient!, dto.Body);
                    break;
                case "push":
                    sent = await SendPushNotificationAsync(dto.Recipient!, dto.Subject, dto.Body);
                    break;
            }

            notification.Status = sent ? "Sent" : "Failed";
            notification.SentAt = sent ? DateTime.UtcNow : null;
            if (!sent)
            {
                notification.ErrorMessage = "Failed to send notification";
            }
        }
        catch (Exception ex)
        {
            notification.Status = "Failed";
            notification.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Failed to send notification {NotificationId}", notificationId);
        }

        await _context.SaveChangesAsync();
        return notification.Status == "Sent";
    }

    public async Task<List<NotificationDto>> GetUserNotificationsAsync(Guid userId, int skip = 0, int take = 20)
    {
        var notifications = await _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return notifications.Select(MapToDto).ToList();
    }

    public async Task<bool> SetNotificationPreferenceAsync(Guid userId, SetNotificationPreferenceDto dto)
    {
        var preference = await _context.Preferences
            .FirstOrDefaultAsync(p => p.UserId == userId 
                && p.Channel == dto.Channel 
                && p.NotificationType == dto.NotificationType);

        if (preference == null)
        {
            preference = new NotificationPreference
            {
                PreferenceId = Guid.NewGuid(),
                UserId = userId,
                Channel = dto.Channel,
                NotificationType = dto.NotificationType,
                IsEnabled = dto.IsEnabled,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Preferences.Add(preference);
        }
        else
        {
            preference.IsEnabled = dto.IsEnabled;
            preference.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<NotificationPreferenceDto>> GetNotificationPreferencesAsync(Guid userId)
    {
        var preferences = await _context.Preferences
            .Where(p => p.UserId == userId)
            .ToListAsync();

        return preferences.Select(p => new NotificationPreferenceDto
        {
            PreferenceId = p.PreferenceId,
            Channel = p.Channel,
            NotificationType = p.NotificationType,
            IsEnabled = p.IsEnabled
        }).ToList();
    }

    private async Task<bool> SendEmailAsync(string recipient, string subject, string body)
    {
        // TODO: Implement email sending (Mailgun, SMTP, etc.)
        var emailEnabled = _configuration.GetValue<bool>("Email:Enabled", false);
        if (!emailEnabled)
        {
            _logger.LogWarning("Email sending is disabled");
            return false;
        }

        // Placeholder for email sending logic
        _logger.LogInformation("Sending email to {Recipient} with subject {Subject}", recipient, subject);
        await Task.Delay(100); // Simulate async operation
        return true;
    }

    private async Task<bool> SendSmsAsync(string recipient, string body)
    {
        // TODO: Implement SMS sending (Vonage, Twilio, etc.)
        var smsEnabled = _configuration.GetValue<bool>("Vonage:Enabled", false);
        if (!smsEnabled)
        {
            _logger.LogWarning("SMS sending is disabled");
            return false;
        }

        // Placeholder for SMS sending logic
        _logger.LogInformation("Sending SMS to {Recipient}", recipient);
        await Task.Delay(100); // Simulate async operation
        return true;
    }

    private async Task<bool> SendPushNotificationAsync(string recipient, string title, string body)
    {
        // TODO: Implement push notification sending (Firebase, APNs, etc.)
        _logger.LogInformation("Sending push notification to {Recipient} with title {Title}", recipient, title);
        await Task.Delay(100); // Simulate async operation
        return true;
    }

    private NotificationTemplateDto MapToDto(NotificationTemplate template)
    {
        return new NotificationTemplateDto
        {
            TemplateId = template.TemplateId,
            Name = template.Name,
            Type = template.Type,
            Subject = template.Subject,
            Body = template.Body,
            Variables = System.Text.Json.JsonSerializer.Deserialize<List<string>>(template.VariablesJson ?? "[]") ?? new(),
            IsActive = template.IsActive,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        };
    }

    private NotificationDto MapToDto(Notification notification)
    {
        return new NotificationDto
        {
            NotificationId = notification.NotificationId,
            UserId = notification.UserId,
            Channel = notification.Channel,
            Type = notification.Type,
            Subject = notification.Subject,
            Body = notification.Body,
            Recipient = notification.Recipient,
            Status = notification.Status,
            ErrorMessage = notification.ErrorMessage,
            SentAt = notification.SentAt,
            DeliveredAt = notification.DeliveredAt,
            CreatedAt = notification.CreatedAt
        };
    }
}

// DTOs
public record CreateNotificationTemplateDto(
    string Name,
    string Type,
    string Subject,
    string Body,
    List<string>? Variables);

public record NotificationTemplateDto
{
    public Guid TemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public List<string> Variables { get; set; } = new();
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public record SendNotificationDto(
    Guid? UserId,
    string Channel,
    string Type,
    string Subject,
    string Body,
    string? Recipient,
    Guid? TemplateId);

public record NotificationDto
{
    public Guid NotificationId { get; set; }
    public Guid? UserId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Recipient { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public record SetNotificationPreferenceDto(
    string Channel,
    string NotificationType,
    bool IsEnabled);

public record NotificationPreferenceDto
{
    public Guid PreferenceId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string NotificationType { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}
