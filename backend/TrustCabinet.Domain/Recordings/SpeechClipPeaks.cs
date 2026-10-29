using System.Buffers.Binary;

namespace TrustCabinet.Domain.Recordings;

/// <summary>
/// The waveform of a sentence the browser sent as 16-bit PCM WAV: the loudest sample of every
/// <see cref="SpeechClipRules.PeakMs"/>, as one byte on a square-root scale so quiet speech still shows. Other formats
/// would need a decoder; they get no waveform.
/// </summary>
public static class SpeechClipPeaks
{
    public static byte[]? FromWav(ReadOnlySpan<byte> wav)
    {
        if (wav.Length < 12 || !wav[..4].SequenceEqual("RIFF"u8) || !wav[8..12].SequenceEqual("WAVE"u8)) return null;
        int channels = 0, rate = 0, bits = 0;
        var at = 12;
        while (at + 8 <= wav.Length)
        {
            var id = wav.Slice(at, 4);
            var size = BinaryPrimitives.ReadInt32LittleEndian(wav.Slice(at + 4, 4));
            if (size < 0) return null;
            var body = wav.Slice(at + 8, Math.Min(size, wav.Length - at - 8));
            if (id.SequenceEqual("fmt "u8) && body.Length >= 16)
            {
                if (BinaryPrimitives.ReadUInt16LittleEndian(body) != 1) return null;
                channels = BinaryPrimitives.ReadUInt16LittleEndian(body[2..]);
                rate = BinaryPrimitives.ReadInt32LittleEndian(body[4..]);
                bits = BinaryPrimitives.ReadUInt16LittleEndian(body[14..]);
            }
            else if (id.SequenceEqual("data"u8))
                return bits == 16 && channels > 0 && rate > 0 ? Peaks(body, channels, rate) : null;
            at += 8 + size + (size & 1);
        }
        return null;
    }

    private static byte[] Peaks(ReadOnlySpan<byte> data, int channels, int rate)
    {
        var frame = 2 * channels;
        var perPeak = Math.Max(1, rate * SpeechClipRules.PeakMs / 1000) * frame;
        var peaks = new byte[(data.Length / frame * frame + perPeak - 1) / perPeak];
        for (var i = 0; i < peaks.Length; i++)
        {
            var window = data.Slice(i * perPeak, Math.Min(perPeak, data.Length - i * perPeak));
            var loudest = 0;
            for (var s = 0; s + 1 < window.Length; s += 2)
                loudest = Math.Max(loudest, Math.Abs((int)BinaryPrimitives.ReadInt16LittleEndian(window[s..])));
            peaks[i] = (byte)Math.Round(255 * Math.Sqrt(Math.Min(loudest, 32767) / 32767.0));
        }
        return peaks;
    }
}
