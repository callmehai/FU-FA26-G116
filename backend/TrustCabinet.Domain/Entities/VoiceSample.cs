using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// One enrolment sample of a person's voice (SCRUM-35), kept only as the speaker model's embedding — never the
/// audio — and only encrypted: <see cref="Ciphertext"/> is AES-256-GCM over the float vector, bound to the person,
/// the sample and the model, so a row copied onto someone else cannot be decrypted.
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
}
