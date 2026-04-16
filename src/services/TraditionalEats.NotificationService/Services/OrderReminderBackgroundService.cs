using Microsoft.EntityFrameworkCore;

namespace TraditionalEats.NotificationService.Services;

public class OrderReminderBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrderReminderBackgroundService> _logger;
    private readonly IConfiguration _configuration;

    public OrderReminderBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<OrderReminderBackgroundService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Order reminder background loop failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task ProcessDueRemindersAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Data.NotificationDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

        var now = DateTime.UtcNow;
        var dueSchedules = await db.OrderReminderSchedules
            .Where(r => r.IsActive && r.NextReminderAt <= now)
            .OrderBy(r => r.NextReminderAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (dueSchedules.Count == 0)
            return;

        foreach (var schedule in dueSchedules)
        {
            await ProcessScheduleAsync(schedule, db, notificationService, httpClientFactory, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessScheduleAsync(
        Entities.OrderReminderSchedule schedule,
        Data.NotificationDbContext db,
        INotificationService notificationService,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        var orderClient = httpClientFactory.CreateClient("OrderService");
        var internalApiKey = _configuration["Services:OrderService:InternalApiKey"];
        if (!string.IsNullOrWhiteSpace(internalApiKey))
        {
            orderClient.DefaultRequestHeaders.Remove("X-Internal-Api-Key");
            orderClient.DefaultRequestHeaders.Add("X-Internal-Api-Key", internalApiKey);
        }

        var statusResponse = await orderClient.GetAsync($"/api/order/internal/{schedule.OrderId}/status", cancellationToken);
        if (!statusResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning("Skipping reminder for OrderId={OrderId}: failed to load status ({StatusCode})", schedule.OrderId, statusResponse.StatusCode);
            schedule.NextReminderAt = DateTime.UtcNow.AddMinutes(1);
            schedule.UpdatedAt = DateTime.UtcNow;
            return;
        }

        var status = await statusResponse.Content.ReadFromJsonAsync<OrderStatusDto>(cancellationToken: cancellationToken);
        if (!string.Equals(status?.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            schedule.IsActive = false;
            schedule.UpdatedAt = DateTime.UtcNow;
            return;
        }

        var restaurantClient = httpClientFactory.CreateClient("RestaurantService");
        var restaurantResponse = await restaurantClient.GetAsync($"/api/restaurant/{schedule.RestaurantId}/notification-recipients", cancellationToken);
        if (!restaurantResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning("Skipping reminder for OrderId={OrderId}: failed to resolve recipients ({StatusCode})", schedule.OrderId, restaurantResponse.StatusCode);
            schedule.NextReminderAt = DateTime.UtcNow.AddMinutes(1);
            schedule.UpdatedAt = DateTime.UtcNow;
            return;
        }

        var recipients = await restaurantResponse.Content.ReadFromJsonAsync<RestaurantNotificationRecipientsDto>(cancellationToken: cancellationToken);
        if (recipients == null)
        {
            schedule.IsActive = false;
            schedule.UpdatedAt = DateTime.UtcNow;
            return;
        }

        var userIds = new List<Guid> { recipients.OwnerUserId };
        userIds.AddRange(recipients.StaffUserIds ?? []);

        var reminderNumber = schedule.ReminderCountSent + 1;
        var orderShort = schedule.OrderId.ToString()[..8];
        var title = "Order reminder";
        var body = $"{recipients.RestaurantName}: Order #{orderShort} is still waiting for action";
        var data = new Dictionary<string, string>
        {
            ["url"] = $"/vendor/orders/{schedule.OrderId}?restaurantId={schedule.RestaurantId}",
            ["orderId"] = schedule.OrderId.ToString(),
            ["restaurantId"] = schedule.RestaurantId.ToString(),
            ["type"] = "order_placed_reminder",
            ["reminderNumber"] = reminderNumber.ToString()
        };

        var sentCount = await notificationService.SendPushToUsersAsync(
            userIds,
            "order_placed_reminder",
            title,
            body,
            data);

        schedule.UpdatedAt = DateTime.UtcNow;
        schedule.NextReminderAt = DateTime.UtcNow.AddMinutes(schedule.IntervalMinutes);

        if (sentCount <= 0)
        {
            _logger.LogWarning(
                "Order reminder push was not delivered for OrderId={OrderId}. Schedule will retry at {NextReminderAt} without incrementing the reminder count.",
                schedule.OrderId,
                schedule.NextReminderAt);
            return;
        }

        schedule.ReminderCountSent = reminderNumber;

        if (schedule.ReminderCountSent >= schedule.MaxReminders)
        {
            schedule.IsActive = false;
        }
    }

    private sealed record OrderStatusDto(Guid OrderId, string Status);

    private sealed record RestaurantNotificationRecipientsDto
    {
        public Guid RestaurantId { get; set; }
        public string RestaurantName { get; set; } = string.Empty;
        public Guid OwnerUserId { get; set; }
        public List<Guid> StaffUserIds { get; set; } = [];
    }
}
