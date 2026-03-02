using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraditionalEats.ChatService.Data;

namespace TraditionalEats.ChatService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminCleanupController : ControllerBase
{
    private readonly ChatDbContext _context;
    private readonly ILogger<AdminCleanupController> _logger;

    public AdminCleanupController(ChatDbContext context, ILogger<AdminCleanupController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>Delete chats older than the specified period. Options: 3, 6, 12, or 24 months.</summary>
    [HttpPost("cleanup/chats")]
    public async Task<IActionResult> CleanupOldChats([FromQuery] int olderThanMonths = 6)
    {
        if (olderThanMonths is not (3 or 6 or 12 or 24))
            return BadRequest(new { message = "olderThanMonths must be 3, 6, 12, or 24" });

        var cutoff = DateTime.UtcNow.AddMonths(-olderThanMonths);

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Order chat: delete messages, then participants for orders with no messages
            var orderIdsToClean = await _context.Messages
                .GroupBy(m => m.OrderId)
                .Where(g => g.Max(m => m.SentAt) < cutoff)
                .Select(g => g.Key)
                .ToListAsync();

            var deletedOrderMessages = await _context.Messages
                .Where(m => orderIdsToClean.Contains(m.OrderId))
                .ExecuteDeleteAsync();

            var deletedParticipants = await _context.Participants
                .Where(p => orderIdsToClean.Contains(p.OrderId))
                .ExecuteDeleteAsync();

            // Vendor chat: delete messages for old conversations, then orphaned conversations
            var convIdsToClean = await _context.VendorConversations
                .Where(c => (c.LastMessageAt ?? c.CreatedAt) < cutoff)
                .Select(c => c.ConversationId)
                .ToListAsync();

            var deletedVendorMessages = await _context.VendorMessages
                .Where(m => convIdsToClean.Contains(m.ConversationId))
                .ExecuteDeleteAsync();

            var deletedConversations = await _context.VendorConversations
                .Where(c => convIdsToClean.Contains(c.ConversationId))
                .ExecuteDeleteAsync();

            await transaction.CommitAsync();

            _logger.LogInformation(
                "Chat cleanup completed: olderThanMonths={Months}, orderMessages={OrderMsg}, participants={Part}, vendorMessages={VendorMsg}, conversations={Conv}",
                olderThanMonths, deletedOrderMessages, deletedParticipants, deletedVendorMessages, deletedConversations);

            return Ok(new
            {
                olderThanMonths,
                cutoff = cutoff.ToString("O"),
                deletedOrderMessages,
                deletedParticipants,
                deletedVendorMessages,
                deletedConversations,
                totalDeleted = deletedOrderMessages + deletedParticipants + deletedVendorMessages + deletedConversations
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Chat cleanup failed");
            return StatusCode(500, new { message = "Chat cleanup failed", error = ex.Message });
        }
    }
}
