using System.Security.Cryptography;
using System.Text;

namespace TrustCabinet.Infrastructure.Ai;

/// <summary>
/// Encrypts the AI API key at rest (ADR-0056): AES-256-GCM, bound to the settings row it belongs to. The key is
/// <c>Ai:EncryptionKey</c> (32 bytes, base64) when set, otherwise derived from the token signing key for this purpose
/// only, as the Voice ID protector does; changing either makes the stored API key unreadable, and the administrator
/// enters it again.
/// </summary>
public sealed class AiKeyProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] key;

    public AiKeyProtector(string configuredKey, byte[] signingKey)
    {
        key = string.IsNullOrWhiteSpace(configuredKey)
            ? HKDF.DeriveKey(HashAlgorithmName.SHA256, signingKey, 32, info: Encoding.UTF8.GetBytes("trustcabinet/ai-api-key/v1"))
            : Convert.FromBase64String(configuredKey);
        if (key.Length != 32) throw new InvalidOperationException("Ai:EncryptionKey must be 32 bytes, base64-encoded.");
    }

    public byte[] Protect(string apiKey, Guid settingId)
    {
        var plain = Encoding.UTF8.GetBytes(apiKey);
        var output = new byte[NonceSize + plain.Length + TagSize];
        var nonce = output.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plain, output.AsSpan(NonceSize, plain.Length), output.AsSpan(NonceSize + plain.Length),
            settingId.ToByteArray());
        CryptographicOperations.ZeroMemory(plain);
        return output;
    }

    public string Unprotect(byte[] ciphertext, Guid settingId)
    {
        var length = ciphertext.Length - NonceSize - TagSize;
        if (length <= 0) throw new CryptographicException("Malformed API key ciphertext.");
        var plain = new byte[length];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(ciphertext.AsSpan(0, NonceSize), ciphertext.AsSpan(NonceSize, length),
            ciphertext.AsSpan(NonceSize + length), plain, settingId.ToByteArray());
        var text = Encoding.UTF8.GetString(plain);
        CryptographicOperations.ZeroMemory(plain);
        return text;
    }
}
