using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Domain.VoiceId;

public static class VoiceIdRules
{
    /// <summary>
    /// Bump when the wording below changes: consent is to the words a person actually read. 1.1 (ADR-0042): the sample
    /// recording is kept too, encrypted, for its owner to hear back. A sample's audio is kept only under a consent to
    /// 1.1 or later; a person who agreed to 1.0 renews to have it kept. 1.2 (ADR-0048): the machine fills in the name
    /// on a sure match (ADR-0045), only for a person whose consent is to 1.2 or later; for the others it still only
    /// suggests. 1.3 (ADR-0060): the same consent lets the Secretary of a meeting the person attends record their samples
    /// on the room's mic (ADR-0060); a person who agreed to an earlier notice renews before a Secretary can.
    /// </summary>
    public const string NoticeVersion = "1.3";

    /// <summary>The first notice under which a sample's recording is kept.</summary>
    public const string AudioNoticeVersion = "1.1";

    /// <summary>The first notice under which the machine names a sure match by itself.</summary>
    public const string AutoAssignNoticeVersion = "1.2";

    /// <summary>The first notice under which a meeting's Secretary may record the person's samples (ADR-0060).</summary>
    public const string CollectionNoticeVersion = "1.3";

    public static bool AllowsCollection(string noticeVersion) => string.CompareOrdinal(noticeVersion, CollectionNoticeVersion) >= 0;

    public const string Purpose =
        "Nhận ra người phát biểu trong bản gỡ băng của các cuộc họp tôi tham dự (gợi ý tên, tự điền tên khi giọng khớp "
        + "rõ), để Thư ký lập biên bản. Giọng mẫu do tôi tự thu, hoặc do Thư ký cuộc họp tôi dự thu bằng micro phòng họp.";

    public const int MaxSamples = 5;
    /// <summary>Samples the meetings' Secretaries may keep for one person on the room's mic (PO 2026-11-21).</summary>
    public const int MaxProxySamples = 50;
    /// <summary>Of those, what one Secretary may take for the same person (PO 2026-11-21).</summary>
    public const int MaxProxySamplesPerCollector = 5;

    /// <summary>True when no more proxy samples may be kept for the person, or none more by this Secretary.</summary>
    public static bool ProxyFull(IReadOnlyCollection<VoiceSample> proxySamples, Guid collectorId)
        => proxySamples.Count >= MaxProxySamples
            || proxySamples.Count(s => s.CollectedById == collectorId) >= MaxProxySamplesPerCollector;
    public const int MaxSampleBytes = 10 * 1024 * 1024;

    public static string RejectionMessage(string reason) => reason switch
    {
        "too_short" => "Giọng mẫu quá ngắn — cần ít nhất 6 giây có tiếng nói. Hãy đọc hết đoạn văn mẫu.",
        "too_quiet" => "Tiếng quá nhỏ — hãy nói to hơn hoặc ngồi gần micro hơn.",
        "clipped" => "Tiếng bị rè vì quá to — hãy để micro xa hơn một chút.",
        "multiple_speakers" => "Nghe như có hơn một người nói — hãy ghi ở nơi yên tĩnh, chỉ mình bạn nói.",
        "too_long" => "Giọng mẫu quá dài — tối đa 60 giây.",
        "unreadable_audio" => "Không đọc được tệp âm thanh.",
        _ => "Giọng mẫu chưa đạt. Hãy ghi lại.",
    };
}
