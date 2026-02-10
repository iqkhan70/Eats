namespace TraditionalEats.DocumentService.Services;

public interface IS3Service
{
    Task<string> UploadFileAsync(
    Stream fileStream,
    string fileName,
    string contentType,
    string folder = "documents",
    CancellationToken cancellationToken = default);
    Task<bool> DeleteFileAsync(string fileUrl, CancellationToken cancellationToken = default);
    Task<string> GetPresignedUrlAsync(string fileUrl, int expirationMinutes = 60);
}
