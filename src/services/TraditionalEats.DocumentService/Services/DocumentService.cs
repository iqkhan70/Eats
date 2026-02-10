using Microsoft.EntityFrameworkCore;
using TraditionalEats.DocumentService.Data;
using TraditionalEats.DocumentService.Entities;

namespace TraditionalEats.DocumentService.Services;

public class DocumentService : IDocumentService
{
    private readonly DocumentDbContext _context;
    private readonly IS3Service _s3Service;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(DocumentDbContext context, IS3Service s3Service, ILogger<DocumentService> logger)
    {
        _context = context;
        _s3Service = s3Service;
        _logger = logger;
    }

    public async Task<Document> UploadDocumentAsync(Guid vendorId, IFormFile file, string documentType, string? notes = null, DateTime? expiresAt = null)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("File is required", nameof(file));
        }

        // Validate file size (max 10MB)
        const long maxFileSize = 10 * 1024 * 1024; // 10MB
        if (file.Length > maxFileSize)
        {
            throw new ArgumentException("File size exceeds 10MB limit", nameof(file));
        }

        // Upload to S3 - ensure stream stays alive during upload
        string fileUrl;
        using (var fileStream = file.OpenReadStream())
        {
            fileUrl = await _s3Service.UploadFileAsync(
                fileStream,
                file.FileName,
                file.ContentType,
                $"vendors/{vendorId}");
        }

        // Create document record
        var document = new Document
        {
            DocumentId = Guid.NewGuid(),
            VendorId = vendorId,
            FileName = Path.GetFileName(fileUrl),
            OriginalFileName = file.FileName,
            FileUrl = fileUrl,
            DocumentType = documentType,
            ContentType = file.ContentType,
            FileSize = file.Length,
            IsActive = true,
            Notes = notes,
            ExpiresAt = expiresAt,
            UploadedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Document uploaded: {DocumentId} by vendor {VendorId}", document.DocumentId, vendorId);
        
        return document;
    }

    public async Task<List<Document>> GetVendorDocumentsAsync(Guid vendorId, bool? isActive = null)
    {
        var query = _context.Documents
            .Where(d => d.VendorId == vendorId);

        if (isActive.HasValue)
        {
            query = query.Where(d => d.IsActive == isActive.Value);
        }

        return await query
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();
    }

    public async Task<Document?> GetDocumentAsync(Guid documentId, Guid vendorId)
    {
        return await _context.Documents
            .FirstOrDefaultAsync(d => d.DocumentId == documentId && d.VendorId == vendorId);
    }

    public async Task<bool> UpdateDocumentStatusAsync(Guid documentId, Guid vendorId, bool isActive)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.DocumentId == documentId && d.VendorId == vendorId);

        if (document == null)
        {
            return false;
        }

        document.IsActive = isActive;
        document.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Document status updated: {DocumentId} to {Status}", documentId, isActive ? "Active" : "Inactive");
        
        return true;
    }

    public async Task<bool> DeleteDocumentAsync(Guid documentId, Guid vendorId)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.DocumentId == documentId && d.VendorId == vendorId);

        if (document == null)
        {
            return false;
        }

        // Delete from S3
        await _s3Service.DeleteFileAsync(document.FileUrl);

        // Delete from database
        _context.Documents.Remove(document);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Document deleted: {DocumentId} by vendor {VendorId}", documentId, vendorId);
        
        return true;
    }

    public async Task<List<Document>> GetAllDocumentsAsync(int skip = 0, int take = 100, Guid? vendorId = null, bool? isActive = null)
    {
        var query = _context.Documents.AsQueryable();

        if (vendorId.HasValue)
        {
            query = query.Where(d => d.VendorId == vendorId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(d => d.IsActive == isActive.Value);
        }

        return await query
            .OrderByDescending(d => d.UploadedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<Document?> GetDocumentByIdAsync(Guid documentId)
    {
        return await _context.Documents
            .FirstOrDefaultAsync(d => d.DocumentId == documentId);
    }

    public async Task<bool> AdminUpdateDocumentStatusAsync(Guid documentId, bool isActive)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.DocumentId == documentId);

        if (document == null)
        {
            return false;
        }

        document.IsActive = isActive;
        document.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Admin updated document status: {DocumentId} to {Status}", documentId, isActive ? "Active" : "Inactive");
        
        return true;
    }

    public async Task<bool> AdminDeleteDocumentAsync(Guid documentId)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.DocumentId == documentId);

        if (document == null)
        {
            return false;
        }

        // Delete from S3
        await _s3Service.DeleteFileAsync(document.FileUrl);

        // Delete from database
        _context.Documents.Remove(document);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Admin deleted document: {DocumentId}", documentId);
        
        return true;
    }
}
