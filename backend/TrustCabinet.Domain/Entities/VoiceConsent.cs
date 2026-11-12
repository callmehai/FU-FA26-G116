using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// A person's consent to Voice ID (SCRUM-35): the system may keep their voice samples and use them to suggest
/// their name for a voice in the meetings they attend.
///
/// A voice used to recognise someone is biometric data — sensitive personal data under Luật BVDLCN 91/2025/QH15
/// and Nghị định 356/2025/NĐ-CP. So the consent is its own record, separate from consent to being recorded,
/// given by the person themself, with the notice they read and the purpose they agreed to stored next to it.
/// Withdrawing it is final for that row and erases every sample taken under it; agreeing again creates a new row.
///
/// A person holds up to two standing consents, one per <see cref="VoiceConsentScope"/>: to enrol their own voice, and
/// to let the Secretary of a meeting they attend record it for them on the room's mic (ADR-0060). Each covers its own
/// set of samples, so either can be withdrawn without touching the other.
/// </summary>
public class VoiceConsent : Entity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Which set of samples this consent covers.</summary>
    public VoiceConsentScope Scope { get; set; } = VoiceConsentScope.Self;

    public DateTimeOffset GrantedAt { get; set; }

    /// <summary>Which wording of the notice was shown, so a later change of wording does not rewrite what was agreed.</summary>
    public required string NoticeVersion { get; set; }

    public required string Purpose { get; set; }

    /// <summary>Null while the consent stands. Set once; a withdrawn consent is never revived.</summary>
    public DateTimeOffset? WithdrawnAt { get; set; }
}

/// <summary>The two sets of voice samples a person can hold, each under its own consent (ADR-0060).</summary>
public enum VoiceConsentScope
{
    /// <summary>Samples the person recorded themself, on their own device.</summary>
    Self,

    /// <summary>
    /// Samples the Secretary of a meeting the person attends recorded for them, on the room's mic. Still the person's
    /// own data: they see, hear and delete every one, and withdrawing erases them all.
    /// </summary>
    Proxy,
}
