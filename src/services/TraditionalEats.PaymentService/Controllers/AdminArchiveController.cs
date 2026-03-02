using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraditionalEats.PaymentService.Data;
using TraditionalEats.PaymentService.Entities;

namespace TraditionalEats.PaymentService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminArchiveController : ControllerBase
{
    private readonly PaymentDbContext _context;
    private readonly ILogger<AdminArchiveController> _logger;

    public AdminArchiveController(PaymentDbContext context, ILogger<AdminArchiveController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>Move payments for the given order IDs to payment_history. Call after OrderService archives orders.</summary>
    [HttpPost("archive/payments")]
    public async Task<IActionResult> ArchivePaymentsForOrders([FromBody] ArchivePaymentsRequest request)
    {
        if (request?.OrderIds == null || request.OrderIds.Count == 0)
            return Ok(new { archivedCount = 0 });

        var paymentsToArchive = await _context.PaymentIntents
            .Where(p => request.OrderIds.Contains(p.OrderId))
            .ToListAsync();

        if (paymentsToArchive.Count == 0)
            return Ok(new { archivedCount = 0 });

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            foreach (var pi in paymentsToArchive)
            {
                _context.PaymentHistory.Add(new PaymentHistory
                {
                    PaymentIntentId = pi.PaymentIntentId,
                    OrderId = pi.OrderId,
                    Amount = pi.Amount,
                    ServiceFee = pi.ServiceFee,
                    Currency = pi.Currency,
                    Status = pi.Status,
                    Provider = pi.Provider,
                    ProviderIntentId = pi.ProviderIntentId,
                    ProviderTransactionId = pi.ProviderTransactionId,
                    FailureReason = pi.FailureReason,
                    CreatedAt = pi.CreatedAt,
                    AuthorizedAt = pi.AuthorizedAt,
                    CapturedAt = pi.CapturedAt,
                    ArchivedAt = DateTime.UtcNow
                });
            }

            await _context.Refunds.Where(r => request.OrderIds.Contains(r.OrderId)).ExecuteDeleteAsync();
            _context.PaymentIntents.RemoveRange(paymentsToArchive);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Archived {Count} payments for {OrderCount} orders", paymentsToArchive.Count, request.OrderIds.Count);
            return Ok(new { archivedCount = paymentsToArchive.Count });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Payment archive failed");
            return StatusCode(500, new { message = "Payment archive failed", error = ex.Message });
        }
    }

    /// <summary>Delete payment history older than X years. Options: 1, 2, 3.</summary>
    [HttpPost("cleanup/payment-history")]
    public async Task<IActionResult> CleanupPaymentHistory([FromQuery] int olderThanYears = 2)
    {
        if (olderThanYears is not (1 or 2 or 3))
            return BadRequest(new { message = "olderThanYears must be 1, 2, or 3" });

        var cutoff = DateTime.UtcNow.AddYears(-olderThanYears);

        var deleted = await _context.PaymentHistory
            .Where(p => p.ArchivedAt < cutoff)
            .ExecuteDeleteAsync();

        _logger.LogInformation("Cleaned up {Count} payment history records older than {Years} years", deleted, olderThanYears);
        return Ok(new { olderThanYears, deletedCount = deleted });
    }
}

public record ArchivePaymentsRequest(List<Guid> OrderIds);
