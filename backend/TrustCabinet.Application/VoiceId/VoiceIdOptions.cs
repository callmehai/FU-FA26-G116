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
    /// ADR-0074: back to the measured values after the 21/11/2026 team test. Lowering every threshold to 0.5 did not
    /// cure the 40 "Người nói N" of a 4-person room — the speech worker's clustering cut was the cause, now fixed there —
    /// and named voices at 0.51–0.55, near-ties between three enrolled people, as one of them.
    /// Lowest cosine similarity at which a voice is suggested as someone. On 24 simulated meetings of real voices (VIVOS,
    /// clean and through a room), 0.5 named the wrong person for 3% of the speech, including people who had not enrolled;
    /// 0.6 named nobody wrongly and still named 97–98% of enrolled people's speech (workers/speech/eval). On the team's
    /// 21/11 room recordings the people in the room scored 0.73–0.88 and every other voice at most 0.66.
    /// </summary>
    public double SuggestThreshold { get; set; } = 0.6;

    /// <summary>
    /// How far the best match must be ahead of the next person to be named by the machine. A near tie is only suggested
    /// (ADR-0087): the Secretary sees "Khớp giọng nhất" and confirms or picks the other person.
    /// </summary>
    public double SuggestMargin { get; set; } = 0.05;

    /// <summary>
    /// How close a second voice suggested as the same person must sound to the first: one person split in two by
    /// diarization, not a stranger who scores well against the same sample. On the same 24 meetings, 0.7 named no
    /// stranger (0.5 named 11% of one) and raised enrolled speech named right from 97% to 99% (workers/speech/eval).
    /// </summary>
    public double SameVoiceThreshold { get; set; } = 0.7;

    /// <summary>
    /// Lowest score for a second voice suggested as a person already suggested in the recording. Two people recorded by
    /// one room mic sounded as alike (0.745) as one person split in two (0.777) on the test server (31/10/2026), so
    /// <see cref="SameVoiceThreshold"/> alone let a stranger at 0.645 be folded into the enrolled person.
    /// </summary>
    public double SecondVoiceThreshold { get; set; } = 0.7;

    /// <summary>
    /// How close an unnamed voice must sound to a person's first voice to be named as that person too, whatever it
    /// scores against the sample: one person split by the clustering. 0.81–0.85 for one person singing on the test
    /// server (03/11/2026); two different people at most 0.745 there and 0.73 in workers/speech/eval.
    /// </summary>
    public double InheritVoiceThreshold { get; set; } = 0.8;

    /// <summary>
    /// Score at which the machine names a voice itself instead of waiting for the Secretary (PO 01/11/2026). The
    /// Secretary can still change or remove the name; the machine never names a guest, who has no enrolled voice.
    /// Equal to <see cref="SuggestThreshold"/> since 22/11/2026 (ADR-0074, was 0.7, before that 0.75): every voice the
    /// machine surely suggests is named, as a sure suggestion already passed the margin over the runner-up and, for a second voice
    /// of the same person, <see cref="SameVoiceThreshold"/> and <see cref="SecondVoiceThreshold"/>. On the 22/11 room
    /// recording the three people were suggested right at 0.61–0.68 and each had to be picked by hand at 0.7.
    /// </summary>
    public double AutoAssignThreshold { get; set; } = 0.6;

    /// <summary>
    /// The organisation that controls the voice data, named in the consent notice: consent is valid only when the person
    /// knows who that is (Luật Bảo vệ dữ liệu cá nhân 91/2025, Điều 9.2.b). Each deployment sets its own legal name.
    /// </summary>
    public string Controller { get; set; } = "";
}
