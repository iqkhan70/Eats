using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Http;
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
    private readonly IHttpClientFactory _httpClientFactory;

    public NotificationService(
        NotificationDbContext context,
        ILogger<NotificationService> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
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
        var emailEnabled = _configuration.GetValue<bool>("Email:Enabled", false);
        if (!emailEnabled)
        {
            _logger.LogWarning("Email sending is disabled");
            return false;
        }

        var emailProvider = _configuration["Email:Provider"] ?? "Mailgun";
        var fromEmail = _configuration["Email:FromEmail"] ?? "noreply@traditionaleats.com";
        var fromName = _configuration["Email:FromName"] ?? "TraditionalEats";

        try
        {
            if (emailProvider.Equals("Mailgun", StringComparison.OrdinalIgnoreCase))
            {
                var mailgunApiKey = _configuration["Email:MailgunApiKey"];
                var mailgunDomain = _configuration["Email:MailgunDomain"];

                if (string.IsNullOrEmpty(mailgunApiKey) || string.IsNullOrEmpty(mailgunDomain))
                {
                    _logger.LogWarning("Mailgun API key or domain is not configured");
                    return false;
                }

                return await SendEmailViaMailgun(recipient, subject, body, fromEmail, fromName, mailgunApiKey, mailgunDomain);
            }
            else
            {
                // Fallback to SMTP
                return await SendEmailViaSmtp(recipient, subject, body, fromEmail, fromName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email to {Recipient}", recipient);
            return false;
        }
    }

    private async Task<bool> SendEmailViaMailgun(string toEmail, string subject, string body, string fromEmail, string fromName, string apiKey, string domain)
    {
        try
        {
            var apiUrl = $"https://api.mailgun.net/v3/{domain}/messages";

            using var httpClient = new HttpClient();
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"api:{apiKey}"));
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);

            var isHtml = body.Contains("<html>") || body.Contains("<body>") || body.Contains("<p>");

            var formData = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("from", $"{fromName} <{fromEmail}>"),
                new KeyValuePair<string, string>("to", toEmail),
                new KeyValuePair<string, string>("subject", subject)
            };

            if (isHtml)
            {
                formData.Add(new KeyValuePair<string, string>("html", body));
            }
            else
            {
                formData.Add(new KeyValuePair<string, string>("text", body));
            }

            var content = new FormUrlEncodedContent(formData);

            _logger.LogInformation("Sending email via Mailgun: Domain={Domain}, To={To}", domain, toEmail);

            var response = await httpClient.PostAsync(apiUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Email sent successfully via Mailgun to {Email}: {Subject}", toEmail, subject);
                return true;
            }
            else
            {
                _logger.LogError("Mailgun API error: Status={Status}, Body={Body}", response.StatusCode, responseBody);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email via Mailgun to {Email}", toEmail);
            return false;
        }
    }

    private async Task<bool> SendEmailViaSmtp(string toEmail, string subject, string body, string fromEmail, string fromName)
    {
        var smtpHost = _configuration["Email:SmtpHost"];
        var smtpPort = _configuration.GetValue<int>("Email:SmtpPort", 587);
        var smtpUsername = _configuration["Email:SmtpUsername"];
        var smtpPassword = _configuration["Email:SmtpPassword"];
        var smtpEnableSsl = _configuration.GetValue<bool>("Email:EnableSsl", true);

        if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(smtpUsername) || string.IsNullOrEmpty(smtpPassword))
        {
            _logger.LogWarning("SMTP configuration is incomplete");
            return false;
        }

        try
        {
            using var mailMessage = new System.Net.Mail.MailMessage();
            mailMessage.From = new System.Net.Mail.MailAddress(fromEmail, fromName);
            mailMessage.To.Add(new System.Net.Mail.MailAddress(toEmail));
            mailMessage.Subject = subject;

            var isHtml = body.Contains("<html>") || body.Contains("<body>") || body.Contains("<p>");
            mailMessage.Body = body;
            mailMessage.IsBodyHtml = isHtml;

            using var smtpClient = new System.Net.Mail.SmtpClient(smtpHost, smtpPort);
            smtpClient.EnableSsl = smtpEnableSsl;
            smtpClient.UseDefaultCredentials = false;
            smtpClient.Credentials = new System.Net.NetworkCredential(smtpUsername, smtpPassword);
            smtpClient.DeliveryMethod = System.Net.Mail.SmtpDeliveryMethod.Network;
            smtpClient.Timeout = 30000;

            await smtpClient.SendMailAsync(mailMessage);
            _logger.LogInformation("Email sent successfully via SMTP to {Email}: {Subject}", toEmail, subject);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email via SMTP to {Email}", toEmail);
            return false;
        }
    }

    private async Task<bool> SendSmsAsync(string recipient, string body)
    {
        var smsEnabled = _configuration.GetValue<bool>("Vonage:Enabled", false);
        if (!smsEnabled)
        {
            _logger.LogWarning("SMS sending is disabled");
            return false;
        }

        var apiKey = _configuration["Vonage:ApiKey"];
        var apiSecret = _configuration["Vonage:ApiSecret"];
        var fromNumber = _configuration["Vonage:FromNumber"];

        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret) || string.IsNullOrEmpty(fromNumber))
        {
            _logger.LogWarning("Vonage configuration is incomplete");
            return false;
        }

        try
        {
            return await SendVonageSmsAsync(recipient, body, apiKey, apiSecret, fromNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SMS to {Recipient}", recipient);
            return false;
        }
    }

    private async Task<bool> SendVonageSmsAsync(string phoneNumber, string message, string apiKey, string apiSecret, string fromNumber)
    {
        try
        {
            _logger.LogInformation("Sending SMS via Vonage API - From: {FromNumber}, To: {PhoneNumber}", fromNumber, phoneNumber);

            var requestBody = new
            {
                from = fromNumber,
                to = phoneNumber,
                text = message,
                type = "text"
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.PostAsync(
                $"https://rest.nexmo.com/sms/json?api_key={apiKey}&api_secret={apiSecret}",
                content);

            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Vonage API Response: Status={StatusCode}, Content={Content}",
                response.StatusCode, responseContent);

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<JsonElement>(responseContent);

                if (result.TryGetProperty("messages", out var messages) && messages.GetArrayLength() > 0)
                {
                    var firstMessage = messages[0];
                    if (firstMessage.TryGetProperty("status", out var status))
                    {
                        var statusValue = status.GetString();
                        _logger.LogInformation("Vonage message status: {Status}", statusValue);
                        return statusValue == "0"; // 0 means success in Vonage API
                    }
                }
            }
            else
            {
                _logger.LogError("Vonage API call failed: {StatusCode} - {Content}", response.StatusCode, responseContent);
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Vonage API call to {PhoneNumber}", phoneNumber);
            return false;
        }
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
