namespace TrustCabinet.Application.VoiceId;

public sealed class VoiceIdOptions
{
    public const string SectionName = "VoiceId";

    /// <summary>
    /// Base64 of 32 random bytes. Empty in development: a key is then derived from the token signing key with its own
    /// label, which keeps a fresh clone working; the test and production servers set their own.
    /// </summary>
    public string EncryptionKey { get; set; } = "";

    /// <summary>
    /// Every threshold here was lowered to 0.5 (margin 0.05) on 21/11/2026 to name more voices; the measurements below
    /// are why the earlier, stricter values were chosen.
    /// Lowest cosine similarity at which a voice is suggested as someone. On 24 simulated meetings of real voices (VIVOS,
    /// clean and through a room), 0.5 named the wrong person for 3% of the speech, including people who had not enrolled;
    /// 0.6 named nobody wrongly and still named 97–98% of enrolled people's speech (workers/speech/eval).
    /// </summary>
    public double SuggestThreshold { get; set; } = 0.5;

    /// <summary>How far the best match must be ahead of the next person, so near ties stay "Người nói N".</summary>
    public double SuggestMargin { get; set; } = 0.05;

    /// <summary>
    /// How close a second voice suggested as the same person must sound to the first: one person split in two by
    /// diarization, not a stranger who scores well against the same sample. On the same 24 meetings, 0.7 named no
    /// stranger (0.5 named 11% of one) and raised enrolled speech named right from 97% to 99% (workers/speech/eval).
    /// </summary>
    public double SameVoiceThreshold { get; set; } = 0.5;

    /// <summary>
    /// Lowest score for a second voice suggested as a person already suggested in the recording. Two people recorded by
    /// one room mic sounded as alike (0.745) as one person split in two (0.777) on the test server (31/10/2026), so
    /// <see cref="SameVoiceThreshold"/> alone let a stranger at 0.645 be folded into the enrolled person.
    /// </summary>
    public double SecondVoiceThreshold { get; set; } = 0.5;

    /// <summary>
    /// How close an unnamed voice must sound to a person's first voice to be named as that person too, whatever it
    /// scores against the sample: one person split by the clustering. 0.81–0.85 for one person singing on the test
    /// server (03/11/2026); two different people at most 0.745 there and 0.73 in workers/speech/eval.
    /// </summary>
    public double InheritVoiceThreshold { get; set; } = 0.5;

    /// <summary>
    /// Score at which the machine names a voice itself instead of waiting for the Secretary (PO 01/11/2026). The
    /// Secretary can still change or remove the name; the machine never names a guest, who has no enrolled voice.
    /// </summary>
    public double AutoAssignThreshold { get; set; } = 0.5;

    /// <summary>
    /// The organisation that controls the voice data, named in the consent notice: consent is valid only when the person
    /// knows who that is (Luật Bảo vệ dữ liệu cá nhân 91/2025, Điều 9.2.b). Each deployment sets its own legal name.
    /// </summary>
    public string Controller { get; set; } = "";
}
