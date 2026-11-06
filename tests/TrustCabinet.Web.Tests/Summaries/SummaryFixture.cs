using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Summaries;
using TrustCabinet.Infrastructure;

namespace TrustCabinet.Web.Tests.Summaries;

/// <summary>
/// The AI draft (SCRUM-36) and the draft minutes (SCRUM-32) against a real PostgreSQL, with the language model replaced
/// by <see cref="FakeLanguageModel"/>. The hosted SummaryWorker is removed by <see cref="TrustCabinetApiFactory"/>; tests
/// drain the queue themselves with <see cref="ProcessAll"/>.
/// </summary>
public sealed class SummaryFixture : IAsyncLifetime
{
    internal readonly PostgreSqlTestDatabase Database = new(PostgresTestDatabase.ConnectionString("trustcabinet_summary_tests"));
    internal readonly TestClock Clock = new();
    internal readonly FakeLanguageModel Model = new();
    internal readonly FailureInterceptor Failures = new();
    internal readonly FakeApi Api = new();
    internal Factory Host = null!;
    private const string SigningKey = "khoa-ky-chi-dung-trong-test-du-32-ky-tu";

    internal TrustCabinetDbContext Db() => new(new DbContextOptionsBuilder<TrustCabinetDbContext>()
        .UseNpgsql(Database.DataSource, o => o.MigrationsHistoryTable("__ef_migrations_history"))
        .UseSnakeCaseNamingConvention().Options);

    public async Task InitializeAsync()
    {
        await Database.CreateFromTemplateAsync(TestSchema.Migrated);
        await using var db = Db();
        await db.Database.MigrateAsync();
        Host = new Factory(this);
        _ = Host.Server;
    }

    public async Task DisposeAsync()
    {
        await Host.DisposeAsync();
        await Database.DisposeAsync();
    }

    internal sealed class Factory(SummaryFixture fixture) : TrustCabinetApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Email:Smtp:Enabled", "false");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TrustCabinetDbContext>();
                services.RemoveAll<DbContextOptions<TrustCabinetDbContext>>();
                services.AddDbContext<TrustCabinetDbContext>(o => o.UseNpgsql(fixture.Database.DataSource,
                    p => p.MigrationsHistoryTable("__ef_migrations_history")).UseSnakeCaseNamingConvention()
                    .AddInterceptors(fixture.Failures));
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(fixture.Clock);
                services.RemoveAll<ILanguageModel>();
                services.AddSingleton<ILanguageModel>(fixture.Model);
                // ADR-0056: the API an administrator may choose answers from the test, never from the network.
                services.AddHttpClient(TrustCabinet.Infrastructure.Ai.LanguageModelProvider.ApiHttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(() => new FakeApi.Handler(fixture.Api));
            });
        }
    }

    /// <summary>
    /// Stands in for the in-house model (ADR-0054): a canned answer for the overview call (sections I and III) and one
    /// for each part of section II, every prompt kept so a test can see what reached the model.
    /// </summary>
    internal sealed class FakeLanguageModel : ILanguageModel
    {
        internal const string Objectives = "Rà soát tiến độ chuyển đổi số quý IV.";
        internal const string Conclusion = "- Báo cáo tiến độ: Thống nhất báo cáo.";
        internal const string Part = "Các đại biểu thảo luận về tiến độ triển khai.";
        internal bool Configured = true;
        internal Exception? Failure;
        internal string OverviewAnswer = $"MỤC TIÊU:\n{Objectives}\nKẾT LUẬN:\n{Conclusion}\nNHIỆM VỤ:\nKhông có.";
        internal string PartAnswer = Part;
        internal readonly ConcurrentQueue<string> Prompts = new();
        /// <summary>Runs once, on the first prompt: what happens to the meeting while the model is writing.</summary>
        internal Func<Task>? While;

        public bool IsConfigured => Configured;
        public string Model => "fake-llm@test";

        public async Task<string> CompleteAsync(string system, string user, CancellationToken ct)
        {
            Prompts.Enqueue(user);
            if (Interlocked.Exchange(ref While, null) is { } meanwhile) await meanwhile();
            if (Failure is { } failure) throw failure;
            return user.Contains("MỤC TIÊU:", StringComparison.Ordinal) ? OverviewAnswer : PartAnswer;
        }

        internal void Reset()
        {
            Configured = true;
            Failure = null;
            While = null;
            OverviewAnswer = $"MỤC TIÊU:\n{Objectives}\nKẾT LUẬN:\n{Conclusion}\nNHIỆM VỤ:\nKhông có.";
            PartAnswer = Part;
            Prompts.Clear();
        }
    }

    /// <summary>
    /// Stands in for an OpenAI-compatible API (ADR-0056): every request kept, the answer the test sets (by default a
    /// chat completion that echoes the overview or a part, like <see cref="FakeLanguageModel"/>).
    /// </summary>
    internal sealed class FakeApi
    {
        internal readonly ConcurrentQueue<(string Url, string? Authorization, string? Session, string Body)> Requests = new();
        internal System.Net.HttpStatusCode Status = System.Net.HttpStatusCode.OK;
        /// <summary>The body of a refusal, when <see cref="Status"/> is not OK.</summary>
        internal string? ErrorBody;
        /// <summary>Answer like a reasoning model that spent its whole budget thinking: no content, finish_reason "length".</summary>
        internal bool CutOff;
        /// <summary>How many calls still answer 429 (with Retry-After: 1) before the API answers again.</summary>
        internal int RateLimited;

        internal void Reset()
        {
            Requests.Clear();
            Status = System.Net.HttpStatusCode.OK;
            ErrorBody = null;
            CutOff = false;
            RateLimited = 0;
        }

        internal sealed class Handler(FakeApi api) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
                api.Requests.Enqueue((request.RequestUri!.ToString(), request.Headers.Authorization?.ToString(),
                    request.Headers.TryGetValues("x-opencode-session", out var session) ? session.Single() : null, body));
                if (api.Status != System.Net.HttpStatusCode.OK)
                    return new HttpResponseMessage(api.Status)
                    {
                        Content = new StringContent(api.ErrorBody ?? "", System.Text.Encoding.UTF8, "application/json"),
                    };
                if (Interlocked.Decrement(ref api.RateLimited) >= 0)
                {
                    var limited = new HttpResponseMessage(System.Net.HttpStatusCode.TooManyRequests);
                    limited.Headers.RetryAfter = new(TimeSpan.FromSeconds(1));
                    return limited;
                }
                if (api.CutOff)
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = System.Net.Http.Json.JsonContent.Create(new { choices = new[] { new
                        {
                            message = new { role = "assistant", content = "", reasoning_content = "Suy nghĩ…" },
                            finish_reason = "length",
                        } } }),
                    };
                // The prompt as the model reads it: the body escapes Vietnamese letters.
                var prompt = body.Length == 0 ? "" : string.Concat(System.Text.Json.JsonDocument.Parse(body).RootElement
                    .GetProperty("messages").EnumerateArray().Select(m => m.GetProperty("content").GetString()));
                var answer = prompt.Contains("MỤC TIÊU:", StringComparison.Ordinal)
                    ? $"MỤC TIÊU:\nMục tiêu do API viết.\nKẾT LUẬN:\n{FakeLanguageModel.Conclusion}\nNHIỆM VỤ:\nKhông có."
                    : "Phần do API viết.";
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = System.Net.Http.Json.JsonContent.Create(new { choices = new[] { new { message = new { role = "assistant", content = answer } } } }),
                };
            }
        }
    }

    internal sealed class TestClock : TimeProvider
    {
        internal DateTimeOffset Now = new(2026, 11, 4, 2, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <summary>A database outage on reads of one table, or on any save; or a hook run once right after a minutes read.</summary>
    internal sealed class FailureInterceptor : DbCommandInterceptor, ISaveChangesInterceptor
    {
        internal bool FailSummaryRead;
        internal bool FailMinutesRead;
        internal bool FailAiSettingsRead;
        internal bool FailSave;
        internal Func<Task>? AfterMinutesRead;

        public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command, CommandEventData eventData,
            InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if ((FailSummaryRead && command.CommandText.Contains("FROM meeting_summaries", StringComparison.Ordinal))
                || (FailMinutesRead && command.CommandText.Contains("FROM meeting_minutes", StringComparison.Ordinal))
                || (FailAiSettingsRead && command.CommandText.Contains("FROM ai_provider_settings", StringComparison.Ordinal)))
                throw new Npgsql.NpgsqlException("Injected read failure.");
            return ValueTask.FromResult(result);
        }

        public override async ValueTask<System.Data.Common.DbDataReader> ReaderExecutedAsync(System.Data.Common.DbCommand command,
            CommandExecutedEventData eventData, System.Data.Common.DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (AfterMinutesRead is { } after && command.CommandText.Contains("FROM meeting_minutes", StringComparison.Ordinal))
            {
                AfterMinutesRead = null;
                await after();
            }
            return result;
        }

        // Saving the minutes, editing or reviewing the draft is one conditional UPDATE (ExecuteUpdate), which bypasses
        // SaveChanges.
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(System.Data.Common.DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => FailSave && (command.CommandText.StartsWith("UPDATE meeting_minutes", StringComparison.Ordinal)
                    || command.CommandText.StartsWith("UPDATE meeting_summaries", StringComparison.Ordinal))
                ? throw new Npgsql.NpgsqlException("Injected persistence failure.")
                : ValueTask.FromResult(result);

        public ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => FailSave ? throw new DbUpdateException("Injected persistence failure.") : ValueTask.FromResult(result);
    }

    internal sealed record Scenario(Guid MeetingId, Guid Secretary, Guid Chair, Guid Member, Guid Organizer, Guid Outsider,
        Guid FirstItem, Guid SecondItem)
    {
        internal string Summary => $"/api/meetings/{MeetingId}/summary";
        internal string Minutes => $"/api/meetings/{MeetingId}/minutes";
    }

    internal const string Title = "Họp giao ban chuyển đổi số";
    internal const string FirstItemTitle = "Báo cáo tiến độ";
    internal const string SecondItemTitle = "Kế hoạch năm 2027";
    internal const string FirstClip = "Tiến độ đạt mười tám trên hai mươi nhiệm vụ.";
    internal const string SecondClip = "Đề nghị bổ sung kinh phí cho năm sau.";
    internal const string VoteTitle = "Thông qua kế hoạch";
    internal const string VoteLine = "Thông qua kế hoạch: Đồng ý 3 phiếu, Không đồng ý 0 phiếu. Kết luận: Thông qua.";

    /// <summary>
    /// A meeting with a Secretary, a Chair and a Member (plus an organizer who is not a participant and an outsider):
    /// two agenda items the Chair started and closed one after the other, one sentence said during each, and one closed
    /// vote. Meetings that have not started get no agenda timing, sentences or vote.
    /// </summary>
    internal async Task<Scenario> Seed(MeetingStatus status = MeetingStatus.InProgress, Sensitivity sensitivity = Sensitivity.Normal)
    {
        await using var db = Db();
        string[] names = ["Nguyễn Thư Ký", "Trần Chủ Trì", "Lê Thành Viên", "Phạm Tổ Chức", "Hoàng Người Ngoài"];
        var users = names.Select(n => new User { FullName = n, Email = $"{Guid.NewGuid():N}@summary.test",
            PasswordHash = "test-only-non-login-hash" }).ToArray();
        db.Users.AddRange(users);
        var start = Clock.Now;
        var started = status is MeetingStatus.InProgress or MeetingStatus.Ended;
        var meeting = new Meeting { Title = Title, CreatedById = users[3].Id, Status = status, Sensitivity = sensitivity,
            Mode = MeetingMode.InPerson, Location = "Phòng họp số 1", PlannedStartTime = start, PlannedEndTime = start.AddHours(1),
            ActualStartedAt = started ? start : null };
        db.Meetings.Add(meeting);
        for (var i = 0; i < 3; i++) db.MeetingParticipants.Add(new() { MeetingId = meeting.Id, UserId = users[i].Id,
            Role = i == 0 ? MeetingRole.Secretary : i == 1 ? MeetingRole.Chair : MeetingRole.Member });
        var first = new AgendaItem { MeetingId = meeting.Id, Position = 1, Title = FirstItemTitle, PresenterId = users[2].Id };
        var second = new AgendaItem { MeetingId = meeting.Id, Position = 2, Title = SecondItemTitle };
        db.AgendaItems.AddRange(first, second);
        if (started)
        {
            first.Status = AgendaItemStatus.Done;
            first.ActualStartedAt = start;
            first.ActualEndedAt = start.AddMinutes(10);
            first.Conclusion = "Thống nhất báo cáo.";
            first.ConclusionUpdatedById = users[1].Id;
            first.ConclusionUpdatedAt = start.AddMinutes(10);
            second.Status = AgendaItemStatus.Done;
            second.ActualStartedAt = start.AddMinutes(10);
            second.ActualEndedAt = start.AddMinutes(20);
            db.SpeechClips.AddRange(
                new SpeechClip { MeetingId = meeting.Id, SpeakerId = users[2].Id, ClientClipId = Guid.NewGuid(),
                    StartedAt = start.AddMinutes(2), DurationMs = 4000, Text = FirstClip },
                new SpeechClip { MeetingId = meeting.Id, SpeakerId = users[1].Id, ClientClipId = Guid.NewGuid(),
                    StartedAt = start.AddMinutes(12), DurationMs = 4000, Text = SecondClip });
            var vote = new VotingSession { MeetingId = meeting.Id, Title = VoteTitle, Status = VotingStatus.Closed,
                CreatedById = users[0].Id, OpenedAt = start.AddMinutes(14), ClosedAt = start.AddMinutes(15), EligibleCount = 3,
                Conclusion = VoteConclusion.Passed, ConcludedById = users[1].Id, ConcludedAt = start.AddMinutes(15) };
            vote.Options.Add(new VotingOption { Position = 1, Label = "Đồng ý", BallotCount = 3 });
            vote.Options.Add(new VotingOption { Position = 2, Label = "Không đồng ý", BallotCount = 0 });
            db.VotingSessions.Add(vote);
        }
        await db.SaveChangesAsync();
        return new(meeting.Id, users[0].Id, users[1].Id, users[2].Id, users[3].Id, users[4].Id, first.Id, second.Id);
    }

    /// <summary>Drains the AI queue the way SummaryWorker does, one draft at a time.</summary>
    internal async Task ProcessAll()
    {
        while (true)
        {
            using var scope = Host.Services.CreateScope();
            if (!await scope.ServiceProvider.GetRequiredService<ISummaryService>().ProcessNextAsync()) return;
        }
    }

    internal HttpClient Client(Guid? actor = null, params string[] capabilities)
    {
        var client = Host.CreateClient();
        if (actor is { } id) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("TrustCabinet", "TrustCabinet",
                [new Claim("sub", id.ToString()), .. capabilities.Select(c => new Claim("cap", c))],
                expires: DateTime.UtcNow.AddMinutes(10),
                signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                    SecurityAlgorithms.HmacSha256))));
        return client;
    }
}
