using Microsoft.EntityFrameworkCore;
using TraditionEats.SupportService.Data;
using TraditionEats.SupportService.Entities;
using System.Text.Json;

namespace TraditionEats.SupportService.Services;

public interface ISupportService
{
    Task<Guid> CreateTicketAsync(Guid userId, CreateTicketDto dto);
    Task<SupportTicketDto?> GetTicketAsync(Guid ticketId);
    Task<List<SupportTicketDto>> GetUserTicketsAsync(Guid userId, int skip = 0, int take = 20);
    Task<List<SupportTicketDto>> GetTicketsAsync(string? status = null, Guid? assignedTo = null, int skip = 0, int take = 20);
    Task<bool> UpdateTicketStatusAsync(Guid ticketId, string status, Guid? assignedTo = null);
    Task<Guid> AddMessageAsync(Guid ticketId, Guid senderId, bool isFromSupport, string content, List<string>? attachments = null);
    Task<List<SupportMessageDto>> GetTicketMessagesAsync(Guid ticketId);
}

public class SupportService : ISupportService
{
    private readonly SupportDbContext _context;
    private readonly ILogger<SupportService> _logger;

    public SupportService(SupportDbContext context, ILogger<SupportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Guid> CreateTicketAsync(Guid userId, CreateTicketDto dto)
    {
        var ticketId = Guid.NewGuid();

        var ticket = new SupportTicket
        {
            TicketId = ticketId,
            UserId = userId,
            OrderId = dto.OrderId,
            Subject = dto.Subject,
            Description = dto.Description,
            Category = dto.Category,
            Status = "Open",
            Priority = dto.Priority ?? "Medium",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created support ticket {TicketId} for user {UserId}", ticketId, userId);
        return ticketId;
    }

    public async Task<SupportTicketDto?> GetTicketAsync(Guid ticketId)
    {
        var ticket = await _context.Tickets
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(t => t.TicketId == ticketId);

        return ticket == null ? null : MapToDto(ticket);
    }

    public async Task<List<SupportTicketDto>> GetUserTicketsAsync(Guid userId, int skip = 0, int take = 20)
    {
        var tickets = await _context.Tickets
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return tickets.Select(MapToDto).ToList();
    }

    public async Task<List<SupportTicketDto>> GetTicketsAsync(string? status = null, Guid? assignedTo = null, int skip = 0, int take = 20)
    {
        var query = _context.Tickets.AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(t => t.Status == status);
        }

        if (assignedTo.HasValue)
        {
            query = query.Where(t => t.AssignedTo == assignedTo.Value);
        }

        var tickets = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return tickets.Select(MapToDto).ToList();
    }

    public async Task<bool> UpdateTicketStatusAsync(Guid ticketId, string status, Guid? assignedTo = null)
    {
        var ticket = await _context.Tickets
            .FirstOrDefaultAsync(t => t.TicketId == ticketId);

        if (ticket == null)
        {
            return false;
        }

        ticket.Status = status;
        if (assignedTo.HasValue)
        {
            ticket.AssignedTo = assignedTo.Value;
        }

        if (status == "Resolved")
        {
            ticket.ResolvedAt = DateTime.UtcNow;
        }

        ticket.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<Guid> AddMessageAsync(Guid ticketId, Guid senderId, bool isFromSupport, string content, List<string>? attachments = null)
    {
        var messageId = Guid.NewGuid();

        var message = new SupportMessage
        {
            MessageId = messageId,
            TicketId = ticketId,
            SenderId = senderId,
            IsFromSupport = isFromSupport,
            Content = content,
            AttachmentsJson = attachments != null ? JsonSerializer.Serialize(attachments) : "[]",
            CreatedAt = DateTime.UtcNow
        };

        _context.Messages.Add(message);

        // Update ticket timestamp
        var ticket = await _context.Tickets.FindAsync(ticketId);
        if (ticket != null)
        {
            ticket.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Added message {MessageId} to ticket {TicketId}", messageId, ticketId);
        return messageId;
    }

    public async Task<List<SupportMessageDto>> GetTicketMessagesAsync(Guid ticketId)
    {
        var messages = await _context.Messages
            .Where(m => m.TicketId == ticketId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        return messages.Select(MapToDto).ToList();
    }

    private SupportTicketDto MapToDto(SupportTicket ticket)
    {
        return new SupportTicketDto
        {
            TicketId = ticket.TicketId,
            UserId = ticket.UserId,
            OrderId = ticket.OrderId,
            Subject = ticket.Subject,
            Description = ticket.Description,
            Category = ticket.Category,
            Status = ticket.Status,
            Priority = ticket.Priority,
            AssignedTo = ticket.AssignedTo,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
            ResolvedAt = ticket.ResolvedAt,
            MessageCount = ticket.Messages?.Count ?? 0
        };
    }

    private SupportMessageDto MapToDto(SupportMessage message)
    {
        return new SupportMessageDto
        {
            MessageId = message.MessageId,
            TicketId = message.TicketId,
            SenderId = message.SenderId,
            IsFromSupport = message.IsFromSupport,
            Content = message.Content,
            Attachments = JsonSerializer.Deserialize<List<string>>(message.AttachmentsJson ?? "[]") ?? new(),
            CreatedAt = message.CreatedAt
        };
    }
}

// DTOs
public record CreateTicketDto(
    Guid? OrderId,
    string Subject,
    string Description,
    string Category,
    string? Priority);

public record SupportTicketDto
{
    public Guid TicketId { get; set; }
    public Guid UserId { get; set; }
    public Guid? OrderId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public Guid? AssignedTo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public int MessageCount { get; set; }
}

public record SupportMessageDto
{
    public Guid MessageId { get; set; }
    public Guid TicketId { get; set; }
    public Guid SenderId { get; set; }
    public bool IsFromSupport { get; set; }
    public string Content { get; set; } = string.Empty;
    public List<string> Attachments { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}
