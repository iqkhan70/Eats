namespace TraditionalEats.PromotionService.Entities;

public class PromotionUsage
{
    public Guid UsageId { get; set; }
    public Guid PromotionId { get; set; }
    public Promotion Promotion { get; set; } = null!;
    public Guid UserId { get; set; }
    public Guid OrderId { get; set; }
    public decimal DiscountAmount { get; set; }
    public DateTime UsedAt { get; set; } = DateTime.UtcNow;
}
