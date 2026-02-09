namespace TraditionalEats.ReviewService.Services;

/// <summary>
/// Service for calculating Elo ratings for restaurants based on reviews.
/// Elo algorithm adapted for restaurant ratings where:
/// - Each restaurant starts at 1500 Elo
/// - Reviews (1-5 stars) are converted to a score (0-1 scale)
/// - Elo is updated: newElo = oldElo + K * (actualScore - expectedScore)
/// </summary>
public static class EloRatingService
{
    // Base Elo rating for new restaurants
    private const decimal BaseEloRating = 1500m;
    
    // K-factor determines how much ratings change per review
    // Higher K = more volatile, Lower K = more stable
    // Using 32 as standard for competitive systems
    private const decimal KFactor = 32m;
    
    // Neutral opponent rating (used for expected score calculation)
    private const decimal NeutralRating = 1500m;

    /// <summary>
    /// Calculates the new Elo rating after a review.
    /// </summary>
    /// <param name="currentElo">Current Elo rating of the restaurant</param>
    /// <param name="starRating">Review rating (1-5 stars)</param>
    /// <returns>New Elo rating</returns>
    public static decimal CalculateNewElo(decimal currentElo, int starRating)
    {
        // Normalize star rating (1-5) to score (0-1)
        // 1 star = 0.0, 2 stars = 0.25, 3 stars = 0.5, 4 stars = 0.75, 5 stars = 1.0
        decimal actualScore = (starRating - 1) / 4.0m;
        
        // Calculate expected score using Elo formula: E = 1 / (1 + 10^((R2 - R1) / 400))
        // Where R1 = restaurant's current Elo, R2 = neutral rating (1500)
        decimal ratingDiff = NeutralRating - currentElo;
        decimal expectedScore = 1m / (1m + (decimal)Math.Pow(10, (double)(ratingDiff / 400m)));
        
        // Calculate new Elo: newElo = oldElo + K * (actualScore - expectedScore)
        decimal newElo = currentElo + KFactor * (actualScore - expectedScore);
        
        // Ensure Elo doesn't go below 0 (though unlikely)
        return Math.Max(0m, newElo);
    }

    /// <summary>
    /// Gets the base Elo rating for new restaurants.
    /// </summary>
    public static decimal GetBaseEloRating() => BaseEloRating;

    /// <summary>
    /// Recalculates Elo rating for a restaurant based on all its reviews.
    /// Useful for recalculating after data corrections or bulk updates.
    /// </summary>
    /// <param name="reviews">All reviews for the restaurant</param>
    /// <param name="currentElo">Current Elo rating (or base if new)</param>
    /// <returns>Recalculated Elo rating</returns>
    public static decimal RecalculateElo(IEnumerable<int> reviews, decimal currentElo = BaseEloRating)
    {
        decimal elo = currentElo;
        foreach (var rating in reviews)
        {
            if (rating >= 1 && rating <= 5)
            {
                elo = CalculateNewElo(elo, rating);
            }
        }
        return elo;
    }
}
