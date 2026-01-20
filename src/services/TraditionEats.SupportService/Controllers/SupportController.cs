using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TraditionEats.SupportService.Services;

namespace TraditionEats.SupportService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SupportController : ControllerBase
{
    private readonly ISupportService _supportService;
    private readonly ILogger<SupportController> _logger;

    public SupportController(ISupportService supportService, ILogger<SupportController> logger)
    {
        _supportService = supportService;
        _logger = logger;
    }

    [HttpPost("tickets")]
    [Authorize]
    public async Task<IActionResult> CreateTicket([FromBody] CreateTicketDto dto)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var ticketId = await _supportService.CreateTicketAsync(userId, dto);
            return Ok(new { ticketId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create ticket");
            return StatusCode(500, new { message = "Failed to create ticket" });
        }
    }

    [HttpGet("tickets/{ticketId}")]
    [Authorize]
    public async Task<IActionResult> GetTicket(Guid ticketId)
    {
        try
        {
            var ticket = await _supportService.GetTicketAsync(ticketId);
            if (ticket == null)
            {
                return NotFound(new { message = "Ticket not found" });
            }
            return Ok(ticket);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get ticket");
            return StatusCode(500, new { message = "Failed to get ticket" });
        }
    }

    [HttpGet("tickets/me")]
    [Authorize]
    public async Task<IActionResult> GetMyTickets(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var tickets = await _supportService.GetUserTicketsAsync(userId, skip, take);
            return Ok(tickets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get tickets");
            return StatusCode(500, new { message = "Failed to get tickets" });
        }
    }

    [HttpGet("tickets")]
    [Authorize(Roles = "Admin,SupportAgent")]
    public async Task<IActionResult> GetTickets(
        [FromQuery] string? status = null,
        [FromQuery] Guid? assignedTo = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20)
    {
        try
        {
            var tickets = await _supportService.GetTicketsAsync(status, assignedTo, skip, take);
            return Ok(tickets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get tickets");
            return StatusCode(500, new { message = "Failed to get tickets" });
        }
    }

    [HttpPatch("tickets/{ticketId}/status")]
    [Authorize(Roles = "Admin,SupportAgent")]
    public async Task<IActionResult> UpdateTicketStatus(Guid ticketId, [FromBody] UpdateTicketStatusRequest request)
    {
        try
        {
            var success = await _supportService.UpdateTicketStatusAsync(
                ticketId, 
                request.Status, 
                request.AssignedTo);
            
            if (!success)
            {
                return NotFound(new { message = "Ticket not found" });
            }
            return Ok(new { message = "Ticket status updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update ticket status");
            return StatusCode(500, new { message = "Failed to update ticket status" });
        }
    }

    [HttpPost("tickets/{ticketId}/messages")]
    [Authorize]
    public async Task<IActionResult> AddMessage(Guid ticketId, [FromBody] AddMessageRequest request)
    {
        try
        {
            var senderId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var isFromSupport = User.IsInRole("Admin") || User.IsInRole("SupportAgent");
            
            var messageId = await _supportService.AddMessageAsync(
                ticketId, 
                senderId, 
                isFromSupport, 
                request.Content, 
                request.Attachments);
            
            return Ok(new { messageId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add message");
            return StatusCode(500, new { message = "Failed to add message" });
        }
    }

    [HttpGet("tickets/{ticketId}/messages")]
    [Authorize]
    public async Task<IActionResult> GetTicketMessages(Guid ticketId)
    {
        try
        {
            var messages = await _supportService.GetTicketMessagesAsync(ticketId);
            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get messages");
            return StatusCode(500, new { message = "Failed to get messages" });
        }
    }
}

public record UpdateTicketStatusRequest(string Status, Guid? AssignedTo = null);
public record AddMessageRequest(string Content, List<string>? Attachments = null);
