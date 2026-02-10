namespace TraditionalEats.DocumentService.Entities;

public class Document
{
    public Guid DocumentId { get; set; }
    public Guid VendorId { get; set; } // UserId from IdentityService (restaurant owner)
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty; // S3 URL
    public string DocumentType { get; set; } = string.Empty; // e.g., "BusinessLicense", "HealthCertificate", "Insurance", etc.
    public string ContentType { get; set; } = string.Empty; // MIME type
    public long FileSize { get; set; } // Size in bytes
    public bool IsActive { get; set; } = true; // Active/Inactive status
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; } // Optional expiration date
    public string? Notes { get; set; } // Optional notes/description
}
