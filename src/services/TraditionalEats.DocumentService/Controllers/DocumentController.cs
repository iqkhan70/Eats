using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TraditionalEats.DocumentService.Services;

namespace TraditionalEats.DocumentService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly IS3Service _s3Service;
    private readonly ILogger<DocumentController> _logger;

    public DocumentController(IDocumentService documentService, IS3Service s3Service, ILogger<DocumentController> logger)
    {
        _documentService = documentService;
        _s3Service = s3Service;
        _logger = logger;
    }

    [HttpPost("upload")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> UploadDocument(
        [FromForm] IFormFile file,
        [FromForm] string documentType,
        [FromForm] string? notes = null,
        [FromForm] DateTime? expiresAt = null)
    {
        try
        {
            var userIdClaim = User?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User?.FindFirstValue("sub");
            
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var vendorId))
            {
                return Unauthorized(new { message = "Invalid or missing user identity" });
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "File is required" });
            }

            var document = await _documentService.UploadDocumentAsync(vendorId, file, documentType, notes, expiresAt);
            
            // Generate presigned URL for immediate access
            var downloadUrl = await _s3Service.GetPresignedUrlAsync(document.FileUrl, 60);
            
            return Ok(new
            {
                documentId = document.DocumentId,
                fileName = document.OriginalFileName,
                documentType = document.DocumentType,
                fileSize = document.FileSize,
                contentType = document.ContentType,
                isActive = document.IsActive,
                uploadedAt = document.UploadedAt,
                expiresAt = document.ExpiresAt,
                downloadUrl = downloadUrl
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error uploading document: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document: {Message}\nStackTrace: {StackTrace}", 
                ex.Message, ex.StackTrace);
            return StatusCode(500, new { message = $"Failed to upload document: {ex.Message}" });
        }
    }

    [HttpGet("vendor/my-documents")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> GetMyDocuments([FromQuery] bool? isActive = null)
    {
        try
        {
            var userIdClaim = User?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User?.FindFirstValue("sub");
            
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var vendorId))
            {
                return Unauthorized(new { message = "Invalid or missing user identity" });
            }

            var documents = await _documentService.GetVendorDocumentsAsync(vendorId, isActive);
            
            // Generate presigned URLs for each document
            var documentsWithUrls = await Task.WhenAll(documents.Select(async doc =>
            {
                var downloadUrl = await _s3Service.GetPresignedUrlAsync(doc.FileUrl, 60);
                return new
                {
                    documentId = doc.DocumentId,
                    fileName = doc.OriginalFileName,
                    documentType = doc.DocumentType,
                    fileSize = doc.FileSize,
                    contentType = doc.ContentType,
                    isActive = doc.IsActive,
                    uploadedAt = doc.UploadedAt,
                    updatedAt = doc.UpdatedAt,
                    expiresAt = doc.ExpiresAt,
                    notes = doc.Notes,
                    downloadUrl = downloadUrl
                };
            }));

            return Ok(documentsWithUrls);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vendor documents");
            return StatusCode(500, new { message = "Failed to fetch documents" });
        }
    }

    [HttpGet("{documentId:guid}")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> GetDocument(Guid documentId)
    {
        try
        {
            var userIdClaim = User?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User?.FindFirstValue("sub");
            
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var vendorId))
            {
                return Unauthorized(new { message = "Invalid or missing user identity" });
            }

            var document = await _documentService.GetDocumentAsync(documentId, vendorId);
            
            if (document == null)
            {
                return NotFound(new { message = "Document not found" });
            }

            var downloadUrl = await _s3Service.GetPresignedUrlAsync(document.FileUrl, 60);
            
            return Ok(new
            {
                documentId = document.DocumentId,
                fileName = document.OriginalFileName,
                documentType = document.DocumentType,
                fileSize = document.FileSize,
                contentType = document.ContentType,
                isActive = document.IsActive,
                uploadedAt = document.UploadedAt,
                updatedAt = document.UpdatedAt,
                expiresAt = document.ExpiresAt,
                notes = document.Notes,
                downloadUrl = downloadUrl
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching document");
            return StatusCode(500, new { message = "Failed to fetch document" });
        }
    }

    [HttpPatch("{documentId:guid}/status")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> UpdateDocumentStatus(Guid documentId, [FromBody] UpdateStatusRequest request)
    {
        try
        {
            var userIdClaim = User?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User?.FindFirstValue("sub");
            
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var vendorId))
            {
                return Unauthorized(new { message = "Invalid or missing user identity" });
            }

            var success = await _documentService.UpdateDocumentStatusAsync(documentId, vendorId, request.IsActive);
            
            if (!success)
            {
                return NotFound(new { message = "Document not found or you don't have permission" });
            }

            return Ok(new { message = $"Document status updated to {(request.IsActive ? "Active" : "Inactive")}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating document status");
            return StatusCode(500, new { message = "Failed to update document status" });
        }
    }

    [HttpDelete("{documentId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteDocument(Guid documentId)
    {
        try
        {
            var success = await _documentService.AdminDeleteDocumentAsync(documentId);
            
            if (!success)
            {
                return NotFound(new { message = "Document not found" });
            }

            return Ok(new { message = "Document deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document");
            return StatusCode(500, new { message = "Failed to delete document" });
        }
    }

    // Admin endpoints
    [HttpGet("admin/all")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllDocuments(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100,
        [FromQuery] Guid? vendorId = null,
        [FromQuery] bool? isActive = null)
    {
        try
        {
            var documents = await _documentService.GetAllDocumentsAsync(skip, take, vendorId, isActive);
            
            // Generate presigned URLs for each document
            var documentsWithUrls = await Task.WhenAll(documents.Select(async doc =>
            {
                var downloadUrl = await _s3Service.GetPresignedUrlAsync(doc.FileUrl, 60);
                return new
                {
                    documentId = doc.DocumentId,
                    vendorId = doc.VendorId,
                    fileName = doc.OriginalFileName,
                    documentType = doc.DocumentType,
                    fileSize = doc.FileSize,
                    contentType = doc.ContentType,
                    isActive = doc.IsActive,
                    uploadedAt = doc.UploadedAt,
                    updatedAt = doc.UpdatedAt,
                    expiresAt = doc.ExpiresAt,
                    notes = doc.Notes,
                    downloadUrl = downloadUrl
                };
            }));

            return Ok(documentsWithUrls);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching all documents");
            return StatusCode(500, new { message = "Failed to fetch documents" });
        }
    }

    [HttpPatch("admin/{documentId:guid}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminUpdateDocumentStatus(Guid documentId, [FromBody] UpdateStatusRequest request)
    {
        try
        {
            var success = await _documentService.AdminUpdateDocumentStatusAsync(documentId, request.IsActive);
            
            if (!success)
            {
                return NotFound(new { message = "Document not found" });
            }

            return Ok(new { message = $"Document status updated to {(request.IsActive ? "Active" : "Inactive")}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating document status");
            return StatusCode(500, new { message = "Failed to update document status" });
        }
    }

    // ----------------------------
    // Menu item image upload (appimages folder, no document record)
    // ----------------------------

    [HttpPost("upload-menu-image")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> UploadMenuImage([FromForm] IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "File is required" });

            var replacePath = Request.Form["replacePath"].FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(replacePath) && replacePath.Contains('%'))
            {
                try { replacePath = Uri.UnescapeDataString(replacePath); } catch { /* keep as-is */ }
            }

            const long maxSize = 5 * 1024 * 1024; // 5MB
            if (file.Length > maxSize)
                return BadRequest(new { message = "Image size must be less than 5MB" });

            string s3Path;
            using (var stream = file.OpenReadStream())
            {
                s3Path = await _s3Service.UploadFileAsync(stream, file.FileName, file.ContentType ?? "image/jpeg", "appimages");
            }

            // Delete previous image from S3 when replacing (avoid orphaned files)
            if (!string.IsNullOrWhiteSpace(replacePath) && replacePath.Contains("appimages", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var deleted = await _s3Service.DeleteFileAsync(replacePath);
                    _logger.LogInformation("Delete previous menu image: Path={Path}, Success={Success}", replacePath, deleted);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete previous menu image {Path}", replacePath);
                }
            }

            return Ok(new { imageUrl = s3Path });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading menu image");
            return StatusCode(500, new { message = "Failed to upload image" });
        }
    }

    [HttpPost("upload-restaurant-image")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> UploadRestaurantImage([FromForm] IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "File is required" });

            var replacePath = Request.Form["replacePath"].FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(replacePath) && replacePath.Contains('%'))
            {
                try { replacePath = Uri.UnescapeDataString(replacePath); } catch { /* keep as-is */ }
            }

            const long maxSize = 5 * 1024 * 1024; // 5MB
            if (file.Length > maxSize)
                return BadRequest(new { message = "Image size must be less than 5MB" });

            string s3Path;
            using (var stream = file.OpenReadStream())
            {
                // Use appimages (same as menu items) so vendor images resolve correctly via menu-image
                s3Path = await _s3Service.UploadFileAsync(stream, file.FileName, file.ContentType ?? "image/jpeg", "appimages");
            }

            // Delete previous image from S3 when replacing (avoid orphaned files)
            if (!string.IsNullOrWhiteSpace(replacePath) && replacePath.Contains("appimages", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var deleted = await _s3Service.DeleteFileAsync(replacePath);
                    _logger.LogInformation("Delete previous restaurant image: Path={Path}, Success={Success}", replacePath, deleted);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete previous restaurant image {Path}", replacePath);
                }
            }

            return Ok(new { imageUrl = s3Path });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading restaurant image");
            return StatusCode(500, new { message = "Failed to upload image" });
        }
    }

    /// <summary>
    /// Deletes an app image from S3 when it is being replaced. Path must be under appimages.
    /// </summary>
    [HttpPost("delete-app-image")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> DeleteAppImage([FromBody] DeleteAppImageRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Path))
            return BadRequest();

        var path = request.Path.Trim();
        if (!path.Contains("appimages", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Invalid image path" });

        try
        {
            var deleted = await _s3Service.DeleteFileAsync(path);
            if (deleted)
                return Ok(new { deleted = true });
            _logger.LogWarning("Delete returned false for path: {Path}", path);
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete app image {Path}", path);
            return StatusCode(500, new { message = "Failed to delete image" });
        }
    }

    /// <summary>
    /// Returns a presigned URL for app images (menu items, vendors). Public - no auth required.
    /// Path must be under appimages to prevent abuse.
    /// </summary>
    [HttpGet("menu-image-url")]
    [AllowAnonymous]
    public async Task<IActionResult> GetMenuImageUrl([FromQuery] string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return BadRequest();

        if (!path.Contains("appimages", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Invalid image path" });

        try
        {
            var url = await _s3Service.GetPresignedUrlAsync(path, 60);
            return Redirect(url);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get presigned URL for path: {Path}", path);
            return NotFound();
        }
    }

    [HttpDelete("admin/{documentId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminDeleteDocument(Guid documentId)
    {
        try
        {
            var success = await _documentService.AdminDeleteDocumentAsync(documentId);
            
            if (!success)
            {
                return NotFound(new { message = "Document not found" });
            }

            return Ok(new { message = "Document deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document");
            return StatusCode(500, new { message = "Failed to delete document" });
        }
    }
}

public record UpdateStatusRequest(bool IsActive);
public record DeleteAppImageRequest(string Path);
