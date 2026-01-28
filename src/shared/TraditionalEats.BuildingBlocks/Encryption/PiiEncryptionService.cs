using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace TraditionalEats.BuildingBlocks.Encryption;

public interface IPiiEncryptionService
{
    string Encrypt(string plaintext, string? keyVersion = null);
    string Decrypt(string ciphertext);
    string HashForSearch(string plaintext);
}

public class PiiEncryptionService : IPiiEncryptionService
{
    private readonly byte[] _masterKey;
    private readonly ILogger<PiiEncryptionService> _logger;

    public PiiEncryptionService(IConfiguration configuration, ILogger<PiiEncryptionService> logger)
    {
        _logger = logger;
        var keyString = configuration["Encryption:MasterKey"]
            ?? throw new InvalidOperationException("Encryption:MasterKey not configured");

        // Support both:
        // - Base64 encoded keys (preferred)
        // - Raw string keys (dev-friendly) → derived to 32 bytes via SHA256
        try
        {
            _masterKey = Convert.FromBase64String(keyString);
        }
        catch (FormatException)
        {
            using var sha = SHA256.Create();
            _masterKey = sha.ComputeHash(Encoding.UTF8.GetBytes(keyString));
            _logger.LogWarning("Encryption:MasterKey is not base64; derived a key via SHA256 (dev fallback).");
        }
    }

    public string Encrypt(string plaintext, string? keyVersion = null)
    {
        if (string.IsNullOrEmpty(plaintext))
            return plaintext;

        using var aes = Aes.Create();
        aes.Key = _masterKey;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var encrypted = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        var result = new
        {
            iv = Convert.ToBase64String(aes.IV),
            ciphertext = Convert.ToBase64String(encrypted),
            keyVersion = keyVersion ?? "v1"
        };

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(result)));
    }

    public string Decrypt(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
            return ciphertext;

        try
        {
            // Backward-compatible fallbacks:
            // - If the value isn't base64 at all, assume it's plaintext (older/dev data) and return as-is.
            // - If it is base64 but not in our JSON envelope format, return the decoded UTF8 string.
            byte[] decodedBytes;
            try
            {
                decodedBytes = Convert.FromBase64String(ciphertext);
            }
            catch (FormatException)
            {
                return ciphertext;
            }

            var decodedText = Encoding.UTF8.GetString(decodedBytes);
            var data = System.Text.Json.JsonSerializer.Deserialize<EncryptionData>(
                decodedText,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (data == null || string.IsNullOrWhiteSpace(data.Iv) || string.IsNullOrWhiteSpace(data.Ciphertext))
            {
                return decodedText;
            }

            using var aes = Aes.Create();
            aes.Key = _masterKey;
            aes.Mode = CipherMode.CBC;
            aes.IV = Convert.FromBase64String(data.Iv);
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            var encryptedBytes = Convert.FromBase64String(data.Ciphertext);
            var decrypted = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);

            return Encoding.UTF8.GetString(decrypted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt PII data");
            throw;
        }
    }

    public string HashForSearch(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return string.Empty;

        var normalized = plaintext.ToLowerInvariant().Trim();
        using var hmac = new HMACSHA256(_masterKey);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToBase64String(hash);
    }

    private record EncryptionData(string? Iv, string? Ciphertext, string? KeyVersion);
}
