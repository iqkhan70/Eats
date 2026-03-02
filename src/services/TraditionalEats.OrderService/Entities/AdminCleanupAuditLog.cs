namespace TraditionalEats.OrderService.Entities;

/// <summary>Audit log for admin data cleanup jobs. Tracks when, who, and what was performed.</summary>
public class AdminCleanupAuditLog
{
    public long Id { get; set; }
    /// <summary>1 = Chat cleanup, 2 = Archive orders/payments, 3 = History cleanup</summary>
    public int JobType { get; set; }
    /// <summary>Parameters used (e.g. {"olderThanMonths":6} or {"olderThanYears":2})</summary>
    public string ParametersJson { get; set; } = "{}";
    /// <summary>Result snapshot (e.g. {"deletedOrderMessages":5,"totalDeleted":10})</summary>
    public string? ResultJson { get; set; }
    public DateTime RanAt { get; set; } = DateTime.UtcNow;
    /// <summary>User ID from JWT (sub claim)</summary>
    public string? RanByUserId { get; set; }
    /// <summary>Email from JWT for readability</summary>
    public string? RanByEmail { get; set; }
    /// <summary>Success or Failed</summary>
    public string Status { get; set; } = "Success";
    /// <summary>Error message if Status = Failed</summary>
    public string? ErrorMessage { get; set; }
}
