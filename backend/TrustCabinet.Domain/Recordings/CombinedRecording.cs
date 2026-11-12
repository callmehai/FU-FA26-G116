using System.Buffers.Binary;

namespace TrustCabinet.Domain.Recordings;

/// <summary>
/// ADR-0062 — a hybrid meeting heard as one track: the room mic's recording and each online attendee's sentences, laid
/// on the real clock and summed. Two people speaking at once are heard at once; an online voice also comes back faintly
/// through the room's speakers, a little late. Every source is 16 kHz mono 16-bit PCM, so mixing is adding samples.
/// </summary>
public interface ICombinedRecordingService
{
    /// <summary>Builds or rebuilds the combined recording of one meeting that needs it; false when none does.</summary>
    Task<bool> BuildNextAsync(CancellationToken ct = default);
}

public static class CombinedRecordingRules
{
    public const int SampleRate = 16_000;
    public const int BytesPerMs = 32;

    /// <summary>Its sources settle first: an online mic's last sentence and the room's last take arrive after the end.</summary>
    public static readonly TimeSpan Settle = TimeSpan.FromMinutes(1);

    /// <summary>Late sentences can arrive up to a week after the end (SpeechClipRules.LateUpload); then nothing changes.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromDays(8);

    public static string FileName(DateTimeOffset startedAt)
        => $"ban-ghi-tong-hop-{TimeZoneInfo.ConvertTime(startedAt, Vietnam):dd-MM-yyyy-HHmm}.wav";

    private static readonly TimeZoneInfo Vietnam = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    /// <summary>The samples of a 16 kHz mono 16-bit WAV, wherever its data chunk sits; null for any other format.</summary>
    public static short[]? ReadPcm(ReadOnlySpan<byte> wav)
    {
        if (wav.Length < 12 || !wav[..4].SequenceEqual("RIFF"u8) || !wav[8..12].SequenceEqual("WAVE"u8)) return null;
        var formatOk = false;
        var at = 12;
        while (at + 8 <= wav.Length)
        {
            var id = wav.Slice(at, 4);
            var size = (int)Math.Min(BinaryPrimitives.ReadUInt32LittleEndian(wav.Slice(at + 4, 4)), (uint)(wav.Length - at - 8));
            var body = wav.Slice(at + 8, size);
            if (id.SequenceEqual("fmt "u8))
            {
                if (body.Length < 16) return null;
                formatOk = BinaryPrimitives.ReadUInt16LittleEndian(body) == 1 // PCM
                    && BinaryPrimitives.ReadUInt16LittleEndian(body[2..]) == 1
                    && BinaryPrimitives.ReadUInt32LittleEndian(body[4..]) == SampleRate
                    && BinaryPrimitives.ReadUInt16LittleEndian(body[14..]) == 16;
            }
            else if (id.SequenceEqual("data"u8))
            {
                if (!formatOk) return null;
                var samples = new short[body.Length / 2];
                for (var i = 0; i < samples.Length; i++) samples[i] = BinaryPrimitives.ReadInt16LittleEndian(body[(i * 2)..]);
                return samples;
            }
            at += 8 + size + (size & 1);
        }
        return null;
    }

    /// <summary>The 44-byte header of a 16 kHz mono 16-bit WAV; its lengths are written once the data is (SealWav).</summary>
    public static byte[] WavHeader()
    {
        var h = new byte[RecordingRules.WavHeaderBytes];
        "RIFF"u8.CopyTo(h);
        "WAVE"u8.CopyTo(h.AsSpan(8));
        "fmt "u8.CopyTo(h.AsSpan(12));
        BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(h.AsSpan(20), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(h.AsSpan(22), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(24), SampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(28), SampleRate * 2);
        BinaryPrimitives.WriteUInt16LittleEndian(h.AsSpan(32), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(h.AsSpan(34), 16);
        "data"u8.CopyTo(h.AsSpan(36));
        return h;
    }

    /// <summary>Sums into 16 bits, clipping rather than wrapping when voices add up past full scale.</summary>
    public static short Clip(int sum) => (short)Math.Clamp(sum, short.MinValue, short.MaxValue);
}
