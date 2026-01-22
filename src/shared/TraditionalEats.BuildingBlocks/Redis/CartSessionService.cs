using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace TraditionalEats.BuildingBlocks.Redis;

/// <summary>
/// Service for managing cart sessions using Redis.
/// Supports both guest carts (by session ID) and authenticated user carts.
/// </summary>
public interface ICartSessionService
{
    /// <summary>
    /// Gets or creates a cart session ID for a guest user.
    /// </summary>
    Task<string> GetOrCreateSessionIdAsync(string? existingSessionId = null);

    /// <summary>
    /// Gets the cart ID for a session (guest cart).
    /// </summary>
    Task<Guid?> GetCartIdForSessionAsync(string sessionId);

    /// <summary>
    /// Stores the cart ID for a session (guest cart).
    /// </summary>
    Task StoreCartIdForSessionAsync(string sessionId, Guid cartId);

    /// <summary>
    /// Gets the cart ID for an authenticated user.
    /// </summary>
    Task<Guid?> GetCartIdForUserAsync(Guid userId);

    /// <summary>
    /// Stores the cart ID for an authenticated user.
    /// </summary>
    Task StoreCartIdForUserAsync(Guid userId, Guid cartId);

    /// <summary>
    /// Merges a guest cart into a user cart.
    /// Returns the merged cart ID (usually the user cart ID, or guest cart ID if user had no cart).
    /// Note: The actual cart item merging should be done by calling OrderService's merge endpoint.
    /// This method only handles the Redis session management.
    /// </summary>
    Task<Guid> MergeGuestCartIntoUserCartAsync(string sessionId, Guid userId, Guid mergedCartId);

    /// <summary>
    /// Clears the session cart after merge.
    /// </summary>
    Task ClearSessionCartAsync(string sessionId);

    /// <summary>
    /// Validates that a session ID is properly formatted (GUID).
    /// </summary>
    bool IsValidSessionId(string? sessionId);
}

public class CartSessionService : ICartSessionService
{
    private readonly IRedisService _redis;
    private readonly ILogger<CartSessionService> _logger;
    private const string SESSION_CART_PREFIX = "cart:session:";
    private const string USER_CART_PREFIX = "cart:user:";
    private static readonly TimeSpan SESSION_TTL = TimeSpan.FromDays(30); // Guest carts expire after 30 days
    private static readonly TimeSpan USER_CART_TTL = TimeSpan.FromDays(90); // User carts expire after 90 days

    public CartSessionService(IRedisService redis, ILogger<CartSessionService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<string> GetOrCreateSessionIdAsync(string? existingSessionId = null)
    {
        // Validate existing session ID if provided
        if (!string.IsNullOrEmpty(existingSessionId) && IsValidSessionId(existingSessionId))
        {
            // Verify the session exists in Redis
            var sessionKey = $"{SESSION_CART_PREFIX}{existingSessionId}";
            if (await _redis.ExistsAsync(sessionKey))
            {
                _logger.LogDebug("Using existing session ID: {SessionId}", existingSessionId);
                return existingSessionId;
            }
            else
            {
                // Session ID is valid but doesn't exist in Redis yet - use it anyway
                // The entry will be created when StoreCartIdForSessionAsync is called
                // This happens when a client sends a new session ID for the first time
                return existingSessionId;
            }
        }

        // Generate new session ID (GUID v4) if none provided
        var newSessionId = Guid.NewGuid().ToString();
        return newSessionId;
    }

    public async Task<Guid?> GetCartIdForSessionAsync(string sessionId)
    {
        if (!IsValidSessionId(sessionId))
        {
            _logger.LogWarning("Invalid session ID format: {SessionId}", sessionId);
            return null;
        }

        var key = $"{SESSION_CART_PREFIX}{sessionId}";
        var cartIdString = await _redis.GetAsync<string>(key);
        
        if (string.IsNullOrEmpty(cartIdString))
        {
            _logger.LogDebug("No cart found for session: {SessionId}", sessionId);
            return null;
        }

        if (Guid.TryParse(cartIdString, out var cartId))
        {
            _logger.LogDebug("Found cart ID {CartId} for session {SessionId}", cartId, sessionId);
            return cartId;
        }

        _logger.LogWarning("Invalid cart ID format in Redis for session {SessionId}: {CartIdString}", sessionId, cartIdString);
        return null;
    }

    public async Task StoreCartIdForSessionAsync(string sessionId, Guid cartId)
    {
        if (!IsValidSessionId(sessionId))
        {
            throw new ArgumentException($"Invalid session ID format: {sessionId}", nameof(sessionId));
        }

        var key = $"{SESSION_CART_PREFIX}{sessionId}";
        await _redis.SetAsync(key, cartId.ToString(), SESSION_TTL);
        _logger.LogDebug("Stored cart ID {CartId} for session {SessionId} with TTL {TTL}", cartId, sessionId, SESSION_TTL);
    }

    public async Task<Guid?> GetCartIdForUserAsync(Guid userId)
    {
        var key = $"{USER_CART_PREFIX}{userId}";
        var cartIdString = await _redis.GetAsync<string>(key);
        
        if (string.IsNullOrEmpty(cartIdString))
        {
            _logger.LogDebug("No cart found for user: {UserId}", userId);
            return null;
        }

        if (Guid.TryParse(cartIdString, out var cartId))
        {
            _logger.LogDebug("Found cart ID {CartId} for user {UserId}", cartId, userId);
            return cartId;
        }

        _logger.LogWarning("Invalid cart ID format in Redis for user {UserId}: {CartIdString}", userId, cartIdString);
        return null;
    }

    public async Task StoreCartIdForUserAsync(Guid userId, Guid cartId)
    {
        var key = $"{USER_CART_PREFIX}{userId}";
        await _redis.SetAsync(key, cartId.ToString(), USER_CART_TTL);
        _logger.LogDebug("Stored cart ID {CartId} for user {UserId} with TTL {TTL}", cartId, userId, USER_CART_TTL);
    }

    public async Task<Guid> MergeGuestCartIntoUserCartAsync(string sessionId, Guid userId, Guid mergedCartId)
    {
        if (!IsValidSessionId(sessionId))
        {
            throw new ArgumentException($"Invalid session ID format: {sessionId}", nameof(sessionId));
        }

        var guestCartId = await GetCartIdForSessionAsync(sessionId);
        var userCartId = await GetCartIdForUserAsync(userId);

        // If no guest cart, return user cart (or use provided mergedCartId)
        if (!guestCartId.HasValue)
        {
            _logger.LogInformation("No guest cart to merge for session {SessionId}", sessionId);
            if (userCartId.HasValue)
            {
                return userCartId.Value;
            }
            // User has no cart, use the provided mergedCartId
            await StoreCartIdForUserAsync(userId, mergedCartId);
            return mergedCartId;
        }

        // If user has no cart, just transfer guest cart to user
        if (!userCartId.HasValue)
        {
            _logger.LogInformation("User {UserId} has no cart, transferring guest cart {GuestCartId} to user", userId, guestCartId.Value);
            await StoreCartIdForUserAsync(userId, mergedCartId);
            await ClearSessionCartAsync(sessionId);
            return mergedCartId;
        }

        // Both carts exist - use the merged cart ID from OrderService
        _logger.LogInformation("Storing merged cart {MergedCartId} for user {UserId} (merged from guest {GuestCartId} and user {UserCartId})", 
            mergedCartId, userId, guestCartId.Value, userCartId.Value);
        
        // Store the merged cart ID for the user
        await StoreCartIdForUserAsync(userId, mergedCartId);
        
        // Clear the guest session cart
        await ClearSessionCartAsync(sessionId);
        
        _logger.LogInformation("Successfully merged carts. Final cart ID: {MergedCartId}", mergedCartId);
        return mergedCartId;
    }

    public async Task ClearSessionCartAsync(string sessionId)
    {
        if (!IsValidSessionId(sessionId))
        {
            return;
        }

        var key = $"{SESSION_CART_PREFIX}{sessionId}";
        await _redis.DeleteAsync(key);
        _logger.LogDebug("Cleared session cart for session {SessionId}", sessionId);
    }

    public bool IsValidSessionId(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return false;

        // Must be a valid GUID format
        return Guid.TryParse(sessionId, out _);
    }
}
