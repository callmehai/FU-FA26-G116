using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// A person's consent to Voice ID (SCRUM-35): the system may keep their voice samples and use them to suggest
/// their name for a voice in the meetings they attend.
///
/// A voice used to recognise someone is biometric data — sensitive personal data under Luật BVDLCN 91/2025/QH15
/// and Nghị định 356/2025/NĐ-CP. So the consent is its own record, separate from consent to being recorded,
/// given by the person themself, with the notice they read and the purpose they agreed to stored next to it.
/// Withdrawing it is final for that row and erases every sample; agreeing again creates a new row.
/// </summary>
public class VoiceConsent : Entity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTimeOffset GrantedAt { get; set; }

    /// <summary>Which wording of the notice was shown, so a later change of wording does not rewrite what was agreed.</summary>
    public required string NoticeVersion { get; set; }

    public required string Purpose { get; set; }

    /// <summary>Null while the consent stands. Set once; a withdrawn consent is never revived.</summary>
    public DateTimeOffset? WithdrawnAt { get; set; }
}
