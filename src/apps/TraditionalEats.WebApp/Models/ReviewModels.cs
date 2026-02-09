namespace TraditionalEats.WebApp.Models;

public class ReviewDto
{
    public Guid ReviewId { get; set; }
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
    public Guid RestaurantId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? Response { get; set; }
    public DateTime? ResponseAt { get; set; }
    public bool IsVerified { get; set; }
    public bool IsVisible { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class RestaurantRatingDto
{
    public Guid RestaurantId { get; set; }
    public decimal AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public Dictionary<int, int> RatingDistribution { get; set; } = new();
}
