using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// One enrolment sample of a person's voice (SCRUM-35), kept only encrypted: <see cref="Ciphertext"/> is AES-256-GCM
/// over the speaker model's embedding, bound to the person, the sample and the model, so a row copied onto someone
/// else cannot be decrypted. As in AI-eCabinet, the recording itself is kept too (<see cref="AudioCiphertext"/>,
/// encrypted the same way) so its owner can hear it back and keep the clear ones; nobody else can play it.
///
/// Erased with every other sample of the person when they withdraw consent.
/// </summary>
public class VoiceSample : Entity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid ConsentId { get; set; }
    public VoiceConsent Consent { get; set; } = null!;

    /// <summary>Nonce, ciphertext and tag, in that order.</summary>
    public required byte[] Ciphertext { get; set; }

    /// <summary>"eres2net-base-200k@&lt;sha12&gt;": an embedding only compares with another from the same model.</summary>
    public required string EmbeddingModel { get; set; }

    /// <summary>How much speech the sample held, after silence was cut.</summary>
    public long SpeechMs { get; set; }

    /// <summary>The recording as uploaded, encrypted (nonce, ciphertext, tag); null for a sample taken before it was kept.</summary>
    public byte[]? AudioCiphertext { get; set; }

    /// <summary>The recording's media type, to play it back as it was sent.</summary>
    public string? AudioContentType { get; set; }
}
