using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;

namespace TraditionalEats.DocumentService.Services;

public class S3Service : IS3Service
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly string _rootFolder;
    private readonly ILogger<S3Service> _logger;

    public S3Service(IAmazonS3 s3Client, IConfiguration configuration, ILogger<S3Service> logger)
    {
        _s3Client = s3Client;
        _logger = logger;

        _bucketName =
            configuration["DigitalOceanSpaces:BucketName"] ??
            configuration["S3:BucketName"] ??
            throw new InvalidOperationException("Bucket name not configured.");

        _rootFolder =
            configuration["DigitalOceanSpaces:Folder"] ??
            configuration["S3:Folder"] ??
            "content";
    }

    // --------------------------------------------------------
    // UPLOAD
    // --------------------------------------------------------
    public async Task<string> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        string folder = "documents",
        CancellationToken cancellationToken = default)
    {
        try
        {
            fileName = SanitizeFileName(fileName);

            var key = $"{_rootFolder}/{folder}/{Guid.NewGuid()}_{fileName}";

            _logger.LogInformation(
                "Uploading file to Spaces. Bucket={Bucket} Key={Key} ContentType={ContentType}",
                _bucketName, key, contentType);

            if (fileStream.CanSeek)
                fileStream.Position = 0;

            var request = new TransferUtilityUploadRequest
            {
                InputStream = fileStream,
                BucketName = _bucketName,
                Key = key,
                ContentType = contentType,

                // Private → access via presigned URL
                CannedACL = S3CannedACL.Private
            };

            var transfer = new TransferUtility(_s3Client);
            await transfer.UploadAsync(request, cancellationToken);

            var result = $"s3://{_bucketName}/{key}";

            _logger.LogInformation("Upload complete: {Key}", key);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload failed for file {FileName}", fileName);
            throw;
        }
    }

    // --------------------------------------------------------
    // DELETE
    // --------------------------------------------------------
    public async Task<bool> DeleteFileAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = ExtractKeyFromUrl(fileUrl);

            if (string.IsNullOrWhiteSpace(key))
            {
                _logger.LogWarning("Delete failed. Could not determine key from {Url}", fileUrl);
                return false;
            }

            await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            }, cancellationToken);

            _logger.LogInformation("Deleted object: {Key}", key);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete failed for {Url}", fileUrl);
            return false;
        }
    }

    // --------------------------------------------------------
    // PRESIGNED URL
    // --------------------------------------------------------
    public Task<string> GetPresignedUrlAsync(
        string fileUrl,
        int expirationMinutes = 60)
    {
        var key = ExtractKeyFromUrl(fileUrl);

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Invalid file URL", nameof(fileUrl));

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(expirationMinutes)
        };

        var url = _s3Client.GetPreSignedURL(request);
        return Task.FromResult(url);
    }

    // --------------------------------------------------------
    // HELPERS
    // --------------------------------------------------------
    private static string SanitizeFileName(string fileName)
    {
        return Path.GetFileName(fileName).Replace(" ", "_");
    }

    private string ExtractKeyFromUrl(string fileUrl)
    {
        // s3://bucket/key
        if (fileUrl.StartsWith("s3://", StringComparison.OrdinalIgnoreCase))
        {
            var withoutScheme = fileUrl.Substring(5);
            var slashIndex = withoutScheme.IndexOf('/');
            return slashIndex >= 0 ? withoutScheme[(slashIndex + 1)..] : "";
        }

        // https://...
        if (Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
        {
            var path = uri.AbsolutePath.TrimStart('/');

            if (path.StartsWith(_bucketName + "/"))
                return path.Substring(_bucketName.Length + 1);

            return path;
        }

        // already key
        return fileUrl;
    }
}
