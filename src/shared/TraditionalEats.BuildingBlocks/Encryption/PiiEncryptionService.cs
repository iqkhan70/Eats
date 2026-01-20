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
        var keyBase64 = configuration["Encryption:MasterKey"] 
            ?? throw new InvalidOperationException("Encryption:MasterKey not configured");
        _masterKey = Convert.FromBase64String(keyBase64);
        _logger = logger;
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
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(ciphertext));
            var data = System.Text.Json.JsonSerializer.Deserialize<EncryptionData>(json)
                ?? throw new InvalidOperationException("Invalid encryption data");

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

    private record EncryptionData(string Iv, string Ciphertext, string KeyVersion);
}
