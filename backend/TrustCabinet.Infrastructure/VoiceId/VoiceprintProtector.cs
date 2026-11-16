using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using TrustCabinet.Application.VoiceId;

namespace TrustCabinet.Infrastructure.VoiceId;

/// <summary>
/// AES-256-GCM over the little-endian float32 vector. The associated data binds the ciphertext to its owner, its
/// sample id and its model: a row moved to another person, or replayed under another id, fails to decrypt.
/// </summary>
public sealed class VoiceprintProtector : IVoiceprintProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] key;

    public VoiceprintProtector(string configuredKey, byte[] signingKey)
    {
        key = string.IsNullOrWhiteSpace(configuredKey)
            ? HKDF.DeriveKey(HashAlgorithmName.SHA256, signingKey, 32, info: Encoding.UTF8.GetBytes("trustcabinet/voiceprint/v1"))
            : Convert.FromBase64String(configuredKey);
        if (key.Length != 32) throw new InvalidOperationException("VoiceId:EncryptionKey must be 32 bytes, base64-encoded.");
    }

    public byte[] Protect(float[] embedding, Guid userId, Guid sampleId, string model)
    {
        var plain = new byte[embedding.Length * 4];
        for (var i = 0; i < embedding.Length; i++) BinaryPrimitives.WriteSingleLittleEndian(plain.AsSpan(i * 4), embedding[i]);
        var output = new byte[NonceSize + plain.Length + TagSize];
        var nonce = output.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plain, output.AsSpan(NonceSize, plain.Length), output.AsSpan(NonceSize + plain.Length), Aad(userId, sampleId, model));
        CryptographicOperations.ZeroMemory(plain);
        return output;
    }

    public float[] Unprotect(byte[] ciphertext, Guid userId, Guid sampleId, string model)
    {
        var length = ciphertext.Length - NonceSize - TagSize;
        if (length <= 0 || length % 4 != 0) throw new CryptographicException("Malformed voiceprint ciphertext.");
        var plain = new byte[length];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(ciphertext.AsSpan(0, NonceSize), ciphertext.AsSpan(NonceSize, length),
            ciphertext.AsSpan(NonceSize + length), plain, Aad(userId, sampleId, model));
        var embedding = new float[length / 4];
        for (var i = 0; i < embedding.Length; i++) embedding[i] = BinaryPrimitives.ReadSingleLittleEndian(plain.AsSpan(i * 4));
        CryptographicOperations.ZeroMemory(plain);
        return embedding;
    }

    /// <summary>The sample's recording, under the same key, bound to its owner and sample apart from the embedding.</summary>
    public byte[] ProtectAudio(byte[] audio, Guid userId, Guid sampleId)
    {
        var output = new byte[NonceSize + audio.Length + TagSize];
        var nonce = output.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, audio, output.AsSpan(NonceSize, audio.Length), output.AsSpan(NonceSize + audio.Length),
            AudioAad(userId, sampleId));
        return output;
    }

    public byte[] UnprotectAudio(byte[] ciphertext, Guid userId, Guid sampleId)
    {
        var length = ciphertext.Length - NonceSize - TagSize;
        if (length <= 0) throw new CryptographicException("Malformed sample audio ciphertext.");
        var plain = new byte[length];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(ciphertext.AsSpan(0, NonceSize), ciphertext.AsSpan(NonceSize, length),
            ciphertext.AsSpan(NonceSize + length), plain, AudioAad(userId, sampleId));
        return plain;
    }

    private static byte[] Aad(Guid userId, Guid sampleId, string model) => Encoding.UTF8.GetBytes($"{userId:N}|{sampleId:N}|{model}");

    private static byte[] AudioAad(Guid userId, Guid sampleId) => Encoding.UTF8.GetBytes($"{userId:N}|{sampleId:N}|audio");
}
