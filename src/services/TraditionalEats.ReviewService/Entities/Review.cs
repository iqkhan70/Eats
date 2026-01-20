namespace TraditionalEats.ReviewService.Entities;

public class Review
{
    public Guid ReviewId { get; set; }
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
    public Guid RestaurantId { get; set; }
    public int Rating { get; set; } // 1-5
    public string? Comment { get; set; }
    public string TagsJson { get; set; } = "[]"; // JSON array stored as string
    public string? Response { get; set; } // Restaurant owner response
    public DateTime? ResponseAt { get; set; }
    public bool IsVerified { get; set; } = false; // Verified purchase
    public bool IsVisible { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
