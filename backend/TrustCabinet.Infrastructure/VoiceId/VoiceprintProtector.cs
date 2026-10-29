using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using TrustCabinet.Domain.VoiceId;

namespace TrustCabinet.Infrastructure.VoiceId;

public sealed class VoiceIdOptions
{
    public const string SectionName = "VoiceId";

    /// <summary>
    /// Base64 of 32 random bytes. Empty in development: a key is then derived from the token signing key with its own
    /// label, which keeps a fresh clone working; the test and production servers set their own.
    /// </summary>
    public string EncryptionKey { get; set; } = "";

    /// <summary>
    /// Lowest cosine similarity at which a voice is suggested as someone. On 24 simulated meetings of real voices (VIVOS,
    /// clean and through a room), 0.5 named the wrong person for 3% of the speech, including people who had not enrolled;
    /// 0.6 named nobody wrongly and still named 97–98% of enrolled people's speech (workers/speech/eval).
    /// </summary>
    public double SuggestThreshold { get; set; } = 0.6;

    /// <summary>How far the best match must be ahead of the next person, so near ties stay "Người nói N".</summary>
    public double SuggestMargin { get; set; } = 0.1;

    /// <summary>
    /// How close a second voice suggested as the same person must sound to the first: one person split in two by
    /// diarization, not a stranger who scores well against the same sample. On the same 24 meetings, 0.7 named no
    /// stranger (0.5 named 11% of one) and raised enrolled speech named right from 97% to 99% (workers/speech/eval).
    /// </summary>
    public double SameVoiceThreshold { get; set; } = 0.7;

    /// <summary>
    /// The organisation that controls the voice data, named in the consent notice: consent is valid only when the person
    /// knows who that is (Luật Bảo vệ dữ liệu cá nhân 91/2025, Điều 9.2.b). Each deployment sets its own legal name.
    /// </summary>
    public string Controller { get; set; } = "";
}

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
