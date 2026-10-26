using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Transcription;
using TrustCabinet.Domain.VoiceId;
using TrustCabinet.Infrastructure;

namespace TrustCabinet.Web.Tests.Recordings;

public sealed class RecordingFixture : IAsyncLifetime
{
    internal readonly PostgreSqlTestDatabase Database = new(PostgresTestDatabase.ConnectionString("trustcabinet_recording_tests"));
    internal readonly TestClock Clock = new();
    internal readonly FailureInterceptor Failures = new();
    internal readonly FakeSpeechEngine Speech = new();
    internal readonly string StorageRoot = Path.Combine(Path.GetTempPath(), $"tc-recordings-{Guid.NewGuid():N}");
    internal Factory Host = null!;
    internal const string SigningKey = "khoa-ky-chi-dung-trong-test-du-32-ky-tu";
    internal const int MaxUploadMegabytes = 1;

    internal TrustCabinetDbContext Db() => new(new DbContextOptionsBuilder<TrustCabinetDbContext>()
        .UseNpgsql(Database.DataSource, o => o.MigrationsHistoryTable("__ef_migrations_history"))
        .UseSnakeCaseNamingConvention().Options);

    public async Task InitializeAsync()
    {
        await using var db = Db();
        await db.Database.MigrateAsync();
        Host = new Factory(this);
        _ = Host.Server;
    }

    public async Task DisposeAsync()
    {
        await Host.DisposeAsync();
        await Database.DisposeAsync();
        if (Directory.Exists(StorageRoot)) Directory.Delete(StorageRoot, true);
    }

    internal sealed class Factory(RecordingFixture fixture) : TrustCabinetApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Recordings:StorageRoot", fixture.StorageRoot);
            builder.UseSetting("Recordings:MaxUploadMegabytes", MaxUploadMegabytes.ToString());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TrustCabinetDbContext>();
                services.RemoveAll<DbContextOptions<TrustCabinetDbContext>>();
                services.AddDbContext<TrustCabinetDbContext>(o => o.UseNpgsql(fixture.Database.DataSource,
                    p => p.MigrationsHistoryTable("__ef_migrations_history")).UseSnakeCaseNamingConvention()
                    .AddInterceptors(fixture.Failures));
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(fixture.Clock);
                services.RemoveAll<ISpeechEngine>();
                services.AddSingleton<ISpeechEngine>(fixture.Speech);
                services.RemoveAll<IVoiceEmbedder>();
                services.AddSingleton<IVoiceEmbedder>(fixture.Speech);
            });
        }
    }

    /// <summary>Stands in for workers/speech: returns what a test sets, or throws what it sets.</summary>
    internal sealed class FakeSpeechEngine : ISpeechEngine, IVoiceEmbedder
    {
        internal SpeechTranscription Result = new(23380, "vi", "gipformer-1.5-68m-int8@abc123def456", "eres2net-base-200k@0123456789ab",
            [new(0, 5800, "SPEAKER_00", "Kính thưa các đồng chí."), new(6500, 10900, "SPEAKER_01", "Tôi đề nghị bổ sung kinh phí.")],
            [new("SPEAKER_00", 5800, [1f, 0f]), new("SPEAKER_01", 4400, [0f, 1f])]);
        internal Exception? Failure;
        /// <summary>Runs while the "worker" is busy, to change the world under a running transcription.</summary>
        internal Func<Task>? During;
        internal int Calls;
        internal byte[]? LastAudio;
        internal bool? LastDiarize;

        public bool IsConfigured => true;

        /// <summary>What the next enrolment sample embeds to; a test sets one vector per sample it posts.</summary>
        internal float[] NextEmbedding = [1f, 0f, 0f];
        internal Exception? EmbedFailure;

        public Task<SpeechEmbedding> EmbedAsync(Stream audio, string contentType, CancellationToken ct)
            => EmbedFailure is { } failure ? Task.FromException<SpeechEmbedding>(failure)
                : Task.FromResult(new SpeechEmbedding(NextEmbedding, "eres2net-base-200k@0123456789ab", 7200));

        public async Task<SpeechTranscription> TranscribeAsync(Stream audio, string contentType, bool diarize, CancellationToken ct)
        {
            Calls++;
            LastDiarize = diarize;
            using var copy = new MemoryStream();
            await audio.CopyToAsync(copy, ct);
            LastAudio = copy.ToArray();
            if (During is { } during) await during();
            return Failure is { } failure ? throw failure : Result;
        }
    }

    internal sealed class TestClock : TimeProvider
    {
        internal DateTimeOffset Now = new(2026, 10, 23, 8, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <summary>Injects a database outage into reads of the recordings table, or into any save.</summary>
    internal sealed class FailureInterceptor : DbCommandInterceptor, ISaveChangesInterceptor
    {
        internal bool FailRead;
        internal bool FailClipRead;
        internal bool FailVoiceRead;
        internal bool FailSave;

        public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command, CommandEventData eventData,
            InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default)
            => (FailRead && command.CommandText.Contains("FROM recordings", StringComparison.Ordinal))
                || (FailVoiceRead && command.CommandText.Contains("FROM voice_consents", StringComparison.Ordinal))
                || (FailClipRead && command.CommandText.Contains("FROM speech_clips", StringComparison.Ordinal))
                ? throw new Npgsql.NpgsqlException("Injected read failure.") : ValueTask.FromResult(result);

        public ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => FailSave ? throw new DbUpdateException("Injected persistence failure.") : ValueTask.FromResult(result);
    }

    internal sealed record Scenario(Guid MeetingId, Guid Secretary, Guid Chair, Guid Member, Guid Organizer, Guid Outsider)
    {
        internal string Url => $"/api/meetings/{MeetingId}/recordings";
    }

    internal async Task<Scenario> Seed(MeetingStatus status = MeetingStatus.InProgress, CaptureMode capture = CaptureMode.PerMic)
    {
        await using var db = Db();
        var users = Enumerable.Range(0, 5).Select(i => new User { FullName = $"Recording test {i}",
            Email = $"{Guid.NewGuid():N}@recording.test", PasswordHash = "test-only-non-login-hash" }).ToArray();
        db.Users.AddRange(users);
        var meeting = new Meeting { Title = "Recording test", CreatedById = users[3].Id, Status = status, CaptureMode = capture,
            PlannedStartTime = Clock.Now, PlannedEndTime = Clock.Now.AddHours(1) };
        db.Meetings.Add(meeting);
        for (var i = 0; i < 3; i++) db.MeetingParticipants.Add(new() { MeetingId = meeting.Id, UserId = users[i].Id,
            Role = i == 0 ? MeetingRole.Secretary : i == 1 ? MeetingRole.Chair : MeetingRole.Member });
        await db.SaveChangesAsync();
        return new(meeting.Id, users[0].Id, users[1].Id, users[2].Id, users[3].Id, users[4].Id);
    }

    internal static string Token(Guid actor) => new JwtSecurityTokenHandler().WriteToken(
        new JwtSecurityToken("TrustCabinet", "TrustCabinet", [new Claim("sub", actor.ToString())],
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256)));

    internal HttpClient Client(Guid? actor = null)
    {
        var client = Host.CreateClient();
        if (actor is { } id) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(id));
        return client;
    }

    internal int StoredFileCount() => Directory.Exists(StorageRoot)
        ? Directory.GetFiles(StorageRoot, "*", SearchOption.AllDirectories).Length : 0;
}
