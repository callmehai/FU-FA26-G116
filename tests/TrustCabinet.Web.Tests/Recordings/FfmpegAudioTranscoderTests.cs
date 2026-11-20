using System.Buffers.Binary;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Recordings;
using TrustCabinet.Infrastructure.Recordings;

namespace TrustCabinet.Web.Tests.Recordings;

/// <summary>
/// ADR-0069 — the real conversion to MP3, where ffmpeg is installed (the image, a developer's machine). CI may have no
/// ffmpeg: those tests then pass without converting, and the endpoints are covered with a fake converter instead.
/// </summary>
public sealed class FfmpegAudioTranscoderTests
{
    private static FfmpegAudioTranscoder Transcoder(string path = "ffmpeg")
        => new(Options.Create(new RecordingOptions { FfmpegPath = path }), NullLogger<FfmpegAudioTranscoder>.Instance);

    /// <summary>Two seconds of a 440 Hz tone, 16 kHz mono 16-bit: what the room mic and the mix store.</summary>
    private static byte[] Tone()
    {
        var samples = 32_000;
        var bytes = new byte[44 + samples * 2];
        CombinedRecordingRules.WavHeader().CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), (uint)(36 + samples * 2));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(40), (uint)(samples * 2));
        for (var i = 0; i < samples; i++)
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(44 + i * 2), (short)(8000 * Math.Sin(2 * Math.PI * 440 * i / 16_000)));
        return bytes;
    }

    private static bool IsMp3(byte[] bytes) => bytes.Length > 1000
        && (bytes.AsSpan(0, 3).SequenceEqual("ID3"u8) || (bytes[0] == 0xFF && (bytes[1] & 0xE0) == 0xE0));

    [Fact]
    public async Task A_stored_wav_comes_out_as_mp3_from_a_file_or_a_pipe()
    {
        var transcoder = Transcoder();
        if (!await transcoder.IsAvailableAsync(CancellationToken.None)) return;
        var path = Path.Combine(Path.GetTempPath(), $"tc-mp3-{Guid.NewGuid():N}.wav");
        await File.WriteAllBytesAsync(path, Tone());
        try
        {
            using var fromFile = new MemoryStream();
            await using (var mp3 = await transcoder.OpenMp3Async(File.OpenRead(path), CancellationToken.None))
                await mp3.CopyToAsync(fromFile);
            Assert.True(IsMp3(fromFile.ToArray()));
            // Two seconds of mono at 64 kbit/s: about 16 KB, a fraction of the 64 KB WAV.
            Assert.InRange(fromFile.Length, 8_000, 40_000);

            using var fromPipe = new MemoryStream();
            await using (var mp3 = await transcoder.OpenMp3Async(new MemoryStream(Tone()), CancellationToken.None))
                await mp3.CopyToAsync(fromPipe);
            Assert.True(IsMp3(fromPipe.ToArray()));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task What_is_not_audio_fails_before_a_byte_is_returned()
    {
        var transcoder = Transcoder();
        if (!await transcoder.IsAvailableAsync(CancellationToken.None)) return;
        await Assert.ThrowsAsync<AudioTranscodeException>(()
            => transcoder.OpenMp3Async(new MemoryStream("not audio at all"u8.ToArray()), CancellationToken.None));
    }

    [Fact]
    public async Task A_missing_ffmpeg_is_reported_as_unavailable()
    {
        var transcoder = Transcoder(Path.Combine(Path.GetTempPath(), $"no-ffmpeg-{Guid.NewGuid():N}"));
        Assert.False(await transcoder.IsAvailableAsync(CancellationToken.None));
        await Assert.ThrowsAsync<AudioTranscodeException>(() => transcoder.OpenMp3Async(new MemoryStream(Tone()), CancellationToken.None));
    }
}
