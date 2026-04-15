namespace TraditionalEats.NotificationService.Entities;

public class OrderReminderSchedule
{
    public Guid OrderReminderScheduleId { get; set; }
    public Guid OrderId { get; set; }
    public Guid RestaurantId { get; set; }
    public int ReminderCountSent { get; set; }
    public int MaxReminders { get; set; } = 5;
    public int IntervalMinutes { get; set; } = 10;
    public DateTime NextReminderAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
