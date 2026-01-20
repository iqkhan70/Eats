using Microsoft.EntityFrameworkCore;
using TraditionEats.BuildingBlocks.Redis;
using TraditionEats.PromotionService.Data;
using TraditionEats.PromotionService.Entities;

namespace TraditionEats.PromotionService.Services;

public interface IPromotionService
{
    Task<Guid> CreatePromotionAsync(CreatePromotionDto dto);
    Task<PromotionDto?> GetPromotionAsync(Guid promotionId);
    Task<PromotionDto?> GetPromotionByCodeAsync(string code);
    Task<List<PromotionDto>> GetPromotionsAsync(Guid? restaurantId = null, bool activeOnly = true);
    Task<bool> ValidatePromotionAsync(string code, Guid userId, decimal orderAmount);
    Task<Guid> ApplyPromotionAsync(string code, Guid userId, Guid orderId, decimal orderAmount);
    Task<decimal> CalculateDiscountAsync(Guid promotionId, decimal orderAmount);
}

public class PromotionService : IPromotionService
{
    private readonly PromotionDbContext _context;
    private readonly IRedisService _redis;
    private readonly ILogger<PromotionService> _logger;

    public PromotionService(
        PromotionDbContext context,
        IRedisService redis,
        ILogger<PromotionService> logger)
    {
        _context = context;
        _redis = redis;
        _logger = logger;
    }

    public async Task<Guid> CreatePromotionAsync(CreatePromotionDto dto)
    {
        var promotionId = Guid.NewGuid();

        var promotion = new Promotion
        {
            PromotionId = promotionId,
            Code = dto.Code.ToUpper(),
            Name = dto.Name,
            Description = dto.Description,
            Type = dto.Type,
            Value = dto.Value,
            MinimumOrderAmount = dto.MinimumOrderAmount,
            MaximumDiscount = dto.MaximumDiscount,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            MaxUses = dto.MaxUses,
            MaxUsesPerUser = dto.MaxUsesPerUser,
            IsActive = true,
            RestaurantId = dto.RestaurantId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Promotions.Add(promotion);
        await _context.SaveChangesAsync();

        // Cache promotion code
        await _redis.SetAsync($"promotion:code:{promotion.Code.ToUpper()}", promotionId, TimeSpan.FromDays(30));

        _logger.LogInformation("Created promotion {PromotionId} with code {Code}", promotionId, dto.Code);
        return promotionId;
    }

    public async Task<PromotionDto?> GetPromotionAsync(Guid promotionId)
    {
        var promotion = await _context.Promotions
            .FirstOrDefaultAsync(p => p.PromotionId == promotionId);

        return promotion == null ? null : MapToDto(promotion);
    }

    public async Task<PromotionDto?> GetPromotionByCodeAsync(string code)
    {
        // Try cache first
        var cachedPromotionId = await _redis.GetAsync<Guid?>($"promotion:code:{code.ToUpper()}");
        if (cachedPromotionId.HasValue)
        {
            return await GetPromotionAsync(cachedPromotionId.Value);
        }

        var promotion = await _context.Promotions
            .FirstOrDefaultAsync(p => p.Code == code.ToUpper());

        if (promotion != null)
        {
            await _redis.SetAsync($"promotion:code:{code.ToUpper()}", promotion.PromotionId, TimeSpan.FromDays(30));
        }

        return promotion == null ? null : MapToDto(promotion);
    }

    public async Task<List<PromotionDto>> GetPromotionsAsync(Guid? restaurantId = null, bool activeOnly = true)
    {
        var query = _context.Promotions.AsQueryable();

        if (restaurantId.HasValue)
        {
            query = query.Where(p => p.RestaurantId == restaurantId || p.RestaurantId == null);
        }
        else
        {
            query = query.Where(p => p.RestaurantId == null); // Platform-wide only
        }

        if (activeOnly)
        {
            var now = DateTime.UtcNow;
            query = query.Where(p => p.IsActive 
                && p.StartDate <= now 
                && p.EndDate >= now);
        }

        var promotions = await query
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return promotions.Select(MapToDto).ToList();
    }

    public async Task<bool> ValidatePromotionAsync(string code, Guid userId, decimal orderAmount)
    {
        var promotion = await GetPromotionByCodeAsync(code);
        if (promotion == null || !promotion.IsActive)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        if (now < promotion.StartDate || now > promotion.EndDate)
        {
            return false;
        }

        if (promotion.MinimumOrderAmount.HasValue && orderAmount < promotion.MinimumOrderAmount.Value)
        {
            return false;
        }

        // Check max uses
        if (promotion.MaxUses.HasValue)
        {
            var usageCount = await _context.PromotionUsages
                .CountAsync(u => u.PromotionId == promotion.PromotionId);
            if (usageCount >= promotion.MaxUses.Value)
            {
                return false;
            }
        }

        // Check max uses per user
        if (promotion.MaxUsesPerUser.HasValue)
        {
            var userUsageCount = await _context.PromotionUsages
                .CountAsync(u => u.PromotionId == promotion.PromotionId && u.UserId == userId);
            if (userUsageCount >= promotion.MaxUsesPerUser.Value)
            {
                return false;
            }
        }

        return true;
    }

    public async Task<Guid> ApplyPromotionAsync(string code, Guid userId, Guid orderId, decimal orderAmount)
    {
        var promotion = await GetPromotionByCodeAsync(code);
        if (promotion == null)
        {
            throw new InvalidOperationException("Promotion not found");
        }

        if (!await ValidatePromotionAsync(code, userId, orderAmount))
        {
            throw new InvalidOperationException("Promotion is not valid");
        }

        var discountAmount = await CalculateDiscountAsync(promotion.PromotionId, orderAmount);

        var usageId = Guid.NewGuid();
        var usage = new PromotionUsage
        {
            UsageId = usageId,
            PromotionId = promotion.PromotionId,
            UserId = userId,
            OrderId = orderId,
            DiscountAmount = discountAmount,
            UsedAt = DateTime.UtcNow
        };

        _context.PromotionUsages.Add(usage);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Applied promotion {Code} to order {OrderId}, discount: {Discount}", 
            code, orderId, discountAmount);
        
        return usageId;
    }

    public async Task<decimal> CalculateDiscountAsync(Guid promotionId, decimal orderAmount)
    {
        var promotion = await _context.Promotions
            .FirstOrDefaultAsync(p => p.PromotionId == promotionId);

        if (promotion == null)
        {
            return 0;
        }

        if (promotion.MinimumOrderAmount.HasValue && orderAmount < promotion.MinimumOrderAmount.Value)
        {
            return 0;
        }

        decimal discount = 0;

        switch (promotion.Type.ToLower())
        {
            case "percentage":
                discount = orderAmount * (promotion.Value / 100);
                if (promotion.MaximumDiscount.HasValue)
                {
                    discount = Math.Min(discount, promotion.MaximumDiscount.Value);
                }
                break;
            case "fixed_amount":
                discount = promotion.Value;
                break;
            case "free_delivery":
                // This would be handled separately
                discount = 0;
                break;
        }

        return Math.Min(discount, orderAmount); // Can't discount more than order amount
    }

    private PromotionDto MapToDto(Promotion promotion)
    {
        return new PromotionDto
        {
            PromotionId = promotion.PromotionId,
            Code = promotion.Code,
            Name = promotion.Name,
            Description = promotion.Description,
            Type = promotion.Type,
            Value = promotion.Value,
            MinimumOrderAmount = promotion.MinimumOrderAmount,
            MaximumDiscount = promotion.MaximumDiscount,
            StartDate = promotion.StartDate,
            EndDate = promotion.EndDate,
            MaxUses = promotion.MaxUses,
            MaxUsesPerUser = promotion.MaxUsesPerUser,
            IsActive = promotion.IsActive,
            RestaurantId = promotion.RestaurantId,
            CreatedAt = promotion.CreatedAt,
            UpdatedAt = promotion.UpdatedAt
        };
    }
}

// DTOs
public record CreatePromotionDto(
    string Code,
    string Name,
    string Description,
    string Type,
    decimal Value,
    decimal? MinimumOrderAmount,
    decimal? MaximumDiscount,
    DateTime StartDate,
    DateTime EndDate,
    int? MaxUses,
    int? MaxUsesPerUser,
    Guid? RestaurantId);

public record PromotionDto
{
    public Guid PromotionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public decimal? MinimumOrderAmount { get; set; }
    public decimal? MaximumDiscount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int? MaxUses { get; set; }
    public int? MaxUsesPerUser { get; set; }
    public bool IsActive { get; set; }
    public Guid? RestaurantId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
