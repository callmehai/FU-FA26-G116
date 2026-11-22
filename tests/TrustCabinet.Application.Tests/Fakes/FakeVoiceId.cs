using System.Security.Cryptography;
using TrustCabinet.Application.VoiceId;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Application.Tests.Fakes;

/// <summary>
/// Consents, samples and meetings in lists. Locks, matching reads and erasures are logged in <see cref="Steps"/>, in
/// order, so a test can assert that a lock came first. Nothing here saves: what is added or removed changes the lists
/// at once.
/// </summary>
public sealed class FakeVoiceIdRepository : IVoiceIdRepository
{
    public List<VoiceConsent> Consents { get; } = [];
    public List<VoiceSample> Samples { get; } = [];
    public List<Meeting> Meetings { get; } = [];
    public List<string> Steps { get; } = [];

    public Task<VoiceConsent?> FindActiveConsentAsync(Guid userId, bool forUpdate, CancellationToken ct)
    {
        if (forUpdate) Steps.Add($"lock consent {userId}");
        return Task.FromResult(Consents.SingleOrDefault(c => c.UserId == userId && c.WithdrawnAt == null));
    }

    public Task<IReadOnlyList<VoiceSample>> ListSamplesAsync(Guid userId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<VoiceSample>>([.. Samples.Where(s => s.UserId == userId).OrderBy(s => s.CreatedAt)]);

    public Task<IReadOnlyList<VoiceSample>> ListProxySamplesAsync(Guid userId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<VoiceSample>>(
            [.. Samples.Where(s => s.UserId == userId && s.CollectedById != null).OrderBy(s => s.CreatedAt)]);

    public Task<IReadOnlyList<VoiceSample>> ListMatchableSamplesAsync(IReadOnlyCollection<Guid> userIds, string embeddingModel,
        CancellationToken ct)
    {
        Steps.Add("read samples to match");
        var matchable = Samples.Where(s => userIds.Contains(s.UserId) && s.EmbeddingModel == embeddingModel).ToList();
        foreach (var sample in matchable) sample.Consent = Consents.Single(c => c.Id == sample.ConsentId);
        return Task.FromResult<IReadOnlyList<VoiceSample>>([.. matchable.Where(s => s.Consent.WithdrawnAt == null)]);
    }

    public Task LockConsentsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        Steps.Add("share-lock consents");
        return Task.CompletedTask;
    }

    public Task<int> DeleteSamplesAsync(Guid consentId, CancellationToken ct)
    {
        Steps.Add("delete samples");
        return Task.FromResult(Samples.RemoveAll(s => s.ConsentId == consentId));
    }

    public Task<int> ClearSuggestionsAsync(Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        Steps.Add("clear suggestions");
        return Task.FromResult(0);
    }

    public Task<IReadOnlyList<VoiceConsentNotice>> ListConsentNoticesAsync(Guid[] userIds, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<VoiceConsentNotice>>([.. Consents
            .Where(c => userIds.Contains(c.UserId) && c.WithdrawnAt == null).Select(c => new VoiceConsentNotice(c.UserId, c.NoticeVersion))]);

    public Task<IReadOnlyList<CollectedSampleRow>> ListCollectedSamplesAsync(Guid[] userIds, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<CollectedSampleRow>>([.. Samples
            .Where(s => userIds.Contains(s.UserId) && s.CollectedById != null)
            .Select(s => new CollectedSampleRow(s.Id, s.UserId, s.MeetingId, s.CreatedAt, s.SpeechMs, s.CollectedBy?.FullName ?? ""))]);

    public Task<VoiceSample?> FindCollectedSampleAsync(Guid meetingId, Guid sampleId, bool tracked, CancellationToken ct)
        => Task.FromResult(Samples.SingleOrDefault(s => s.Id == sampleId && s.MeetingId == meetingId && s.CollectedById != null));

    public Task<VoiceSample?> FindProxySampleAsync(Guid userId, Guid sampleId, bool tracked, CancellationToken ct)
        => Task.FromResult(Samples.SingleOrDefault(s => s.Id == sampleId && s.UserId == userId && s.CollectedById != null));

    public Task<Meeting?> FindMeetingWithPeopleAsync(Guid meetingId, CancellationToken ct)
        => Task.FromResult(Meetings.SingleOrDefault(m => m.Id == meetingId));

    public void Add(VoiceConsent consent) => Consents.Add(consent);
    public void Add(VoiceSample sample) => Samples.Add(sample);
    public void Remove(VoiceSample sample) => Samples.Remove(sample);
}

/// <summary>Answers <see cref="Embedding"/> for every sample, or throws <see cref="Fail"/>; counts the calls.</summary>
public sealed class FakeVoiceEmbedder : IVoiceEmbedder
{
    public SpeechEmbedding Embedding { get; set; } = new([1f, 0f], "test-model", 3000);
    public Exception? Fail { get; set; }
    public int Calls { get; private set; }

    public Task<SpeechEmbedding> EmbedAsync(Stream audio, string contentType, CancellationToken ct)
    {
        Calls++;
        return Fail is { } failure ? Task.FromException<SpeechEmbedding>(failure) : Task.FromResult(Embedding);
    }
}

/// <summary>
/// Keeps each embedding by its sample id and hands back an opaque marker as the ciphertext; a sample it never protected
/// fails to decrypt, as one under a rotated key. Audio is "encrypted" by appending one byte.
/// </summary>
public sealed class FakeVoiceprintProtector : IVoiceprintProtector
{
    private readonly Dictionary<Guid, float[]> _embeddings = [];

    public byte[] Protect(float[] embedding, Guid userId, Guid sampleId, string model)
    {
        _embeddings[sampleId] = embedding;
        return [0xE0];
    }

    public float[] Unprotect(byte[] ciphertext, Guid userId, Guid sampleId, string model)
        => _embeddings.TryGetValue(sampleId, out var embedding) ? embedding : throw new CryptographicException("unknown sample");

    public byte[] ProtectAudio(byte[] audio, Guid userId, Guid sampleId) => [.. audio, 0xA0];

    /// <summary>The key changed: no kept recording decrypts any more.</summary>
    public bool AudioUnreadable { get; set; }

    public byte[] UnprotectAudio(byte[] ciphertext, Guid userId, Guid sampleId)
        => AudioUnreadable ? throw new CryptographicException("key changed") : ciphertext[..^1];
}
