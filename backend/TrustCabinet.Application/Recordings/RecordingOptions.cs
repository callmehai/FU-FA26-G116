namespace TrustCabinet.Application.Recordings;

public sealed class RecordingOptions
{
    public const string SectionName = "Recordings";

    /// <summary>
    /// Where audio files are written. Relative paths resolve against the app's content root; the test server mounts
    /// a volume here so a redeploy does not erase every recording.
    /// </summary>
    public string StorageRoot { get; set; } = "App_Data/recordings";

    /// <summary>
    /// How long a recording is kept before it is erased automatically — the "thời hạn lưu trữ" Luật 91/2025 asks to be
    /// stated at collection. Long enough to write and approve the minutes, short enough not to become an archive.
    /// </summary>
    public int RetentionDays { get; set; } = 180;

    /// <summary>Three hours of mp3 or one of uncompressed 16 kHz WAV fit with room to spare.</summary>
    public int MaxUploadMegabytes { get; set; } = 300;

    /// <summary><see cref="MaxUploadMegabytes"/> in bytes: what the server refuses past, and what the list tells the
    /// browser so it refuses a bigger file before sending it.</summary>
    public long MaxUploadBytes => MaxUploadMegabytes * 1024L * 1024;

    /// <summary>How long a signed playback URL works. Covers listening through a long meeting without re-signing.</summary>
    public int PlaybackUrlMinutes { get; set; } = 120;

    /// <summary>
    /// The room mic's part, in bytes: ten seconds of 16 kHz PCM. What has not reached the server yet waits on the
    /// device, so a smaller part only means more requests.
    /// </summary>
    public int RoomPartBytes { get; set; } = 320_000;

    /// <summary>A take no part has reached for this long is saved by the server: the laptop is not coming back.</summary>
    public int IdleTakeHours { get; set; } = 12;

    /// <summary>
    /// ADR-0069 — the ffmpeg that converts a download to MP3. A bare name is looked up on PATH; the image installs it
    /// there.
    /// </summary>
    public string FfmpegPath { get; set; } = "ffmpeg";

    /// <summary>MP3 bitrate of a download, in kbit/s. Mono speech stays clear at 64: about 29 MB an hour.</summary>
    public int Mp3Kbps { get; set; } = 64;
}
