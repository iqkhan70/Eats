using TraditionalEats.DocumentService.Entities;

namespace TraditionalEats.DocumentService.Services;

public interface IDocumentService
{
    Task<Document> UploadDocumentAsync(Guid vendorId, IFormFile file, string documentType, string? notes = null, DateTime? expiresAt = null);
    Task<List<Document>> GetVendorDocumentsAsync(Guid vendorId, bool? isActive = null);
    Task<Document?> GetDocumentAsync(Guid documentId, Guid vendorId);
    Task<bool> UpdateDocumentStatusAsync(Guid documentId, Guid vendorId, bool isActive);
    Task<bool> DeleteDocumentAsync(Guid documentId, Guid vendorId);
    
    // Admin methods
    Task<List<Document>> GetAllDocumentsAsync(int skip = 0, int take = 100, Guid? vendorId = null, bool? isActive = null);
    Task<Document?> GetDocumentByIdAsync(Guid documentId);
    Task<bool> AdminUpdateDocumentStatusAsync(Guid documentId, bool isActive);
    Task<bool> AdminDeleteDocumentAsync(Guid documentId);
}
