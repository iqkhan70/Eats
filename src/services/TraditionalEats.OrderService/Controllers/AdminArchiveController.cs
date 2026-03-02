using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraditionalEats.OrderService.Data;
using TraditionalEats.OrderService.Entities;

namespace TraditionalEats.OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminArchiveController : ControllerBase
{
    private readonly OrderDbContext _context;
    private readonly ILogger<AdminArchiveController> _logger;

    public AdminArchiveController(OrderDbContext context, ILogger<AdminArchiveController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>Move orders older than X months to order_history. Options: 6, 12, 24. Returns archived OrderIds for PaymentService.</summary>
    [HttpPost("archive/orders")]
    public async Task<IActionResult> ArchiveOldOrders([FromQuery] int olderThanMonths = 12)
    {
        if (olderThanMonths is not (6 or 12 or 24))
            return BadRequest(new { message = "olderThanMonths must be 6, 12, or 24" });

        var cutoff = DateTime.UtcNow.AddMonths(-olderThanMonths);

        var ordersToArchive = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .Where(o => o.CreatedAt < cutoff)
            .ToListAsync();

        if (ordersToArchive.Count == 0)
            return Ok(new { archivedCount = 0, orderIds = Array.Empty<Guid>() });

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var orderIds = new List<Guid>();
            foreach (var order in ordersToArchive)
            {
                var history = new OrderHistory
                {
                    OrderId = order.OrderId,
                    CustomerId = order.CustomerId,
                    RestaurantId = order.RestaurantId,
                    Subtotal = order.Subtotal,
                    Tax = order.Tax,
                    DeliveryFee = order.DeliveryFee,
                    ServiceFee = order.ServiceFee,
                    Total = order.Total,
                    Status = order.Status,
                    CreatedAt = order.CreatedAt,
                    EstimatedDeliveryAt = order.EstimatedDeliveryAt,
                    DeliveredAt = order.DeliveredAt,
                    PaidAt = order.PaidAt,
                    PaymentStatus = order.PaymentStatus,
                    StripePaymentIntentId = order.StripePaymentIntentId,
                    PaymentFailureReason = order.PaymentFailureReason,
                    DeliveryAddress = order.DeliveryAddress,
                    SpecialInstructions = order.SpecialInstructions,
                    ArchivedAt = DateTime.UtcNow,
                    Items = order.Items.Select(i => new OrderItemHistory
                    {
                        OrderItemId = i.OrderItemId,
                        OrderId = i.OrderId,
                        MenuItemId = i.MenuItemId,
                        Name = i.Name,
                        Description = i.Description,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        TotalPrice = i.TotalPrice,
                        ModifiersJson = i.ModifiersJson
                    }).ToList(),
                    StatusHistory = order.StatusHistory.Select(h => new OrderStatusHistoryArchive
                    {
                        Id = h.Id,
                        OrderId = h.OrderId,
                        Status = h.Status,
                        Notes = h.Notes,
                        ChangedAt = h.ChangedAt
                    }).ToList()
                };
                _context.OrderHistory.Add(history);
                _context.OrderItemHistory.AddRange(history.Items);
                _context.OrderStatusHistoryArchive.AddRange(history.StatusHistory);

                _context.OrderStatusHistory.RemoveRange(order.StatusHistory);
                _context.OrderItems.RemoveRange(order.Items);
                _context.Orders.Remove(order);

                orderIds.Add(order.OrderId);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Archived {Count} orders older than {Months} months", orderIds.Count, olderThanMonths);
            return Ok(new { archivedCount = orderIds.Count, orderIds });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Order archive failed");
            return StatusCode(500, new { message = "Order archive failed", error = ex.Message });
        }
    }

    /// <summary>Delete order history older than X years. Options: 1, 2, 3.</summary>
    [HttpPost("cleanup/order-history")]
    public async Task<IActionResult> CleanupOrderHistory([FromQuery] int olderThanYears = 2)
    {
        if (olderThanYears is not (1 or 2 or 3))
            return BadRequest(new { message = "olderThanYears must be 1, 2, or 3" });

        var cutoff = DateTime.UtcNow.AddYears(-olderThanYears);

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var orderIdsToDelete = await _context.OrderHistory
                .Where(o => o.ArchivedAt < cutoff)
                .Select(o => o.OrderId)
                .ToListAsync();

            await _context.OrderStatusHistoryArchive.Where(h => orderIdsToDelete.Contains(h.OrderId)).ExecuteDeleteAsync();
            await _context.OrderItemHistory.Where(i => orderIdsToDelete.Contains(i.OrderId)).ExecuteDeleteAsync();
            var deletedOrders = await _context.OrderHistory.Where(o => orderIdsToDelete.Contains(o.OrderId)).ExecuteDeleteAsync();

            await transaction.CommitAsync();

            _logger.LogInformation("Cleaned up {Count} order history records older than {Years} years", deletedOrders, olderThanYears);
            return Ok(new { olderThanYears, deletedCount = deletedOrders });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Order history cleanup failed");
            return StatusCode(500, new { message = "Order history cleanup failed", error = ex.Message });
        }
    }

    /// <summary>Get recent admin cleanup audit log entries.</summary>
    [HttpGet("audit/cleanup")]
    public async Task<IActionResult> GetCleanupAuditLog([FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        var logs = await _context.AdminCleanupAuditLogs
            .OrderByDescending(l => l.RanAt)
            .Skip(skip)
            .Take(Math.Min(take, 200))
            .Select(l => new { l.Id, l.JobType, l.ParametersJson, l.ResultJson, l.RanAt, l.RanByUserId, l.RanByEmail, l.Status, l.ErrorMessage })
            .ToListAsync();
        return Ok(logs);
    }

    /// <summary>Record an admin cleanup audit log entry. Called by BFF after each cleanup job.</summary>
    [HttpPost("audit/cleanup")]
    public async Task<IActionResult> RecordCleanupAudit([FromBody] RecordCleanupAuditRequest request)
    {
        if (request == null)
            return BadRequest(new { message = "Request body required" });
        if (request.JobType is not (1 or 2 or 3))
            return BadRequest(new { message = "JobType must be 1, 2, or 3" });

        var userId = request.RanByUserId ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = request.RanByEmail ?? User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");

        var parametersJson = request.ParametersJson ?? "{}";
        if (parametersJson.Length > 512) parametersJson = parametersJson[..504] + "...";
        var resultJson = request.ResultJson;
        if (!string.IsNullOrEmpty(resultJson) && resultJson.Length > 2048) resultJson = resultJson[..2040] + "...";

        var log = new AdminCleanupAuditLog
        {
            JobType = request.JobType,
            ParametersJson = parametersJson,
            ResultJson = resultJson,
            RanAt = DateTime.UtcNow,
            RanByUserId = userId,
            RanByEmail = email,
            Status = request.Status ?? "Success",
            ErrorMessage = request.ErrorMessage
        };
        _context.AdminCleanupAuditLogs.Add(log);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Audit logged: JobType={JobType}, Status={Status}, RanBy={Email}", request.JobType, request.Status, email);
        return Ok(new { id = log.Id });
    }
}

public record RecordCleanupAuditRequest(
    int JobType,
    string? ParametersJson,
    string? ResultJson,
    string? Status,
    string? ErrorMessage,
    string? RanByUserId,
    string? RanByEmail);
