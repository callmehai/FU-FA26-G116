using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Infrastructure;
using TrustCabinet.Web.Tests.Summaries;
using TrustCabinet.Web.Tests.Users;
using static TrustCabinet.Web.Tests.Agenda.AgendaEndpointTests;

namespace TrustCabinet.Web.Tests.Audit;

/// <summary>
/// SCRUM-16, UC-13 — GET /api/audit-log and /api/audit-log/event-types against a real PostgreSQL, and the write side the
/// log depends on: a row per lifecycle move (BR-445) and the append-only guard (BR-440).
/// </summary>
public sealed class AuditLogEndpointTests(SummaryFixture f) : IClassFixture<SummaryFixture>
{
    private const string Url = "/api/audit-log";
    private static readonly DateTimeOffset Base = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    private sealed record Log(Guid Admin, Guid User, Guid Role, Guid Invitation, string Marker);

    /// <summary>
    /// Eight rows a minute apart, oldest first, all carrying <see cref="Log.Marker"/>: an account change, a role change,
    /// a Guest under each of the four keys writers have used, the system, and an account acting on a Guest's sentence.
    /// </summary>
    private async Task<Log> Seed()
    {
        var s = await f.Seed();
        await using var db = f.Db();
        var role = new Role { Name = "Kiểm toán " + Guid.NewGuid().ToString("N")[..8] };
        var admin = new User { FullName = "Phạm Quản Trị", Email = $"{Guid.NewGuid():N}@audit.test", PasswordHash = "x", RoleId = role.Id };
        var user = new User { FullName = "Trần Thị Bích", Email = $"{Guid.NewGuid():N}@audit.test", PasswordHash = "x" };
        var invitation = new MeetingGuestInvitation { MeetingId = s.MeetingId, InvitedName = "Khách Văn Mời",
            Email = $"{Guid.NewGuid():N}@guests.test", InvitedById = s.Secretary };
        db.Roles.Add(role);
        db.RoleCapabilities.Add(new RoleCapability { RoleId = role.Id, Capability = Capability.AdminUsers });
        db.Users.AddRange(admin, user);
        db.MeetingGuestInvitations.Add(invitation);
        var marker = "m" + Guid.NewGuid().ToString("N");
        var guest = new MeetingGuestSession(s.MeetingId, invitation.Id, Guid.NewGuid());
        (Guid? Actor, string Action, string Type, Guid? Object, object Details)[] rows =
        [
            (admin.Id, AuditActions.UserUpdated, AuditActions.UserObjectType, user.Id, new { marker, IsActive = false }),
            (admin.Id, AuditActions.RoleCreated, AuditActions.RoleObjectType, role.Id, new { marker }),
            (null, AuditActions.DocumentViewed, AuditActions.MeetingDocumentObjectType, Guid.NewGuid(),
                new { marker, guestInvitationId = invitation.Id }),
            (null, AuditActions.CallGuestAdmitted, AuditActions.GuestSessionObjectType, guest.SessionId,
                new { marker, invitationId = invitation.Id }),
            (null, AuditActions.SpeechClipDeleted, AuditActions.SpeechClipObjectType, Guid.NewGuid(),
                new { marker, GuestInvitationId = invitation.Id }),
            (null, AuditActions.MeetingParticipants(ParticipantOperation.Respond), AuditActions.MeetingObjectType, s.MeetingId,
                new { marker, guest }),
            (null, AuditActions.VoteDraftDeletedOnMeetingEnd, AuditActions.VoteObjectType, Guid.NewGuid(), new { marker }),
            (user.Id, AuditActions.SpeechClipDeleted, AuditActions.SpeechClipObjectType, Guid.NewGuid(),
                new { marker, GuestInvitationId = invitation.Id }),
        ];
        db.AuditLog.AddRange(rows.Select((r, i) => new AuditLogEntry
        {
            ActorId = r.Actor, Action = r.Action, ObjectType = r.Type, ObjectId = r.Object, IpAddress = "203.0.113.7",
            Details = JsonSerializer.Serialize(r.Details), OccurredAt = Base.AddMinutes(i),
        }));
        await db.SaveChangesAsync();
        return new(admin.Id, user.Id, role.Id, invitation.Id, marker);
    }

    private HttpClient Admin(Log log) => f.Client(log.Admin, nameof(Capability.AdminUsers));

    private static async Task<JsonElement> Page(HttpClient client, string query)
    {
        var response = await client.GetAsync($"{Url}?{query}");
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, raw);
        return JsonDocument.Parse(raw).RootElement.Clone();
    }

    private static string[] Actions(JsonElement page)
        => page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("action").GetString()!).ToArray();

    [Fact]
    public async Task Only_a_role_holding_user_administration_reads_the_log()
    {
        var log = await Seed();
        var s = await f.Seed();
        using var anonymous = f.Client();
        using var secretary = f.Client(s.Secretary, nameof(Capability.MeetingCreator));
        // The token claims the capability, the database says the role does not hold it (BR-440, EX-3).
        using var claims = f.Client(log.User, nameof(Capability.AdminUsers));
        foreach (var path in new[] { Url, Url + "/event-types" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await secretary.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await claims.GetAsync(path)).StatusCode);
        }
    }

    [Fact]
    public async Task Entries_come_newest_first_with_each_actor_named_as_an_account_a_guest_or_the_system()
    {
        var log = await Seed();
        using var admin = Admin(log);

        var page = await Page(admin, $"q={log.Marker}");

        Assert.Equal(8, page.GetProperty("totalCount").GetInt32());
        var items = page.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(Base.AddMinutes(7), items[0].GetProperty("occurredAt").GetDateTimeOffset());
        Assert.Equal(["user", "system", "guest", "guest", "guest", "guest", "user", "user"],
            items.Select(i => i.GetProperty("actor").GetProperty("kind").GetString()));
        // A Guest is named from the invitation, whichever key the writer used (BR-441).
        foreach (var guest in items[2..6].Select(i => i.GetProperty("actor")))
        {
            Assert.Equal(log.Invitation, guest.GetProperty("id").GetGuid());
            Assert.Equal("Khách Văn Mời", guest.GetProperty("name").GetString());
            Assert.EndsWith("@guests.test", guest.GetProperty("email").GetString());
        }
        var system = items[1].GetProperty("actor");
        Assert.Equal(JsonValueKind.Null, system.GetProperty("id").ValueKind);
        Assert.Equal(JsonValueKind.Null, system.GetProperty("name").ValueKind);

        var change = items[7];
        Assert.Equal("Phạm Quản Trị", change.GetProperty("actor").GetProperty("name").GetString());
        Assert.Equal("Sửa tài khoản", change.GetProperty("actionLabel").GetString());
        Assert.Equal("User", change.GetProperty("objectType").GetString());
        Assert.Equal("Trần Thị Bích", change.GetProperty("objectName").GetString());
        Assert.Equal("203.0.113.7", change.GetProperty("ipAddress").GetString());
        Assert.False(change.GetProperty("details").GetProperty("IsActive").GetBoolean());
        Assert.StartsWith("Kiểm toán ", items[6].GetProperty("objectName").GetString());
        // Nothing about the meeting is looked up (BR-443).
        Assert.Equal(JsonValueKind.Null, items[2].GetProperty("objectName").ValueKind);
        Assert.Equal(JsonValueKind.Null, items[5].GetProperty("objectName").ValueKind);
    }

    [Fact]
    public async Task Every_filter_narrows_and_all_must_hold()
    {
        var log = await Seed();
        using var admin = Admin(log);
        var q = $"q={log.Marker}";

        Assert.Equal(4, (await Page(admin, $"{q}&actor=guest")).GetProperty("totalCount").GetInt32());
        Assert.Equal([AuditActions.VoteDraftDeletedOnMeetingEnd], Actions(await Page(admin, $"{q}&actor=system")));
        Assert.Equal([AuditActions.SpeechClipDeleted], Actions(await Page(admin, $"{q}&actor={log.User}")));
        Assert.Equal([AuditActions.RoleCreated, AuditActions.UserUpdated], Actions(await Page(admin, $"{q}&actor={log.Admin}")));
        Assert.Equal([AuditActions.RoleCreated], Actions(await Page(admin, $"{q}&action={AuditActions.RoleCreated}")));
        Assert.Equal([AuditActions.SpeechClipDeleted],
            Actions(await Page(admin, $"{q}&action={AuditActions.SpeechClipDeleted}&actor=guest")));
        // Both ends count; the offset is honoured.
        var from = Uri.EscapeDataString(Base.AddMinutes(1).ToOffset(TimeSpan.FromHours(7)).ToString("O"));
        var to = Uri.EscapeDataString(Base.AddMinutes(2).ToString("O"));
        Assert.Equal([AuditActions.DocumentViewed, AuditActions.RoleCreated], Actions(await Page(admin, $"{q}&from={from}&to={to}")));
        // The keyword matches the object id as well as the details, and a typed % is itself, not a wildcard.
        Assert.Contains(AuditActions.RoleCreated, Actions(await Page(admin, $"q={log.Role}")));
        Assert.Equal(0, (await Page(admin, $"q={log.Marker}%25")).GetProperty("totalCount").GetInt32());
        Assert.Equal(8, (await Page(admin, $"q={log.Marker.ToUpperInvariant()}")).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task The_keyword_matches_recorded_values_never_key_names()
    {
        var log = await Seed();
        var key = "k" + Guid.NewGuid().ToString("N");
        var path = $@"C:\tai-lieu\{Guid.NewGuid():N}";
        await using (var db = f.Db())
        {
            db.AuditLog.Add(new AuditLogEntry
            {
                ActorId = log.Admin, Action = AuditActions.PersonalLibraryRead, ObjectType = AuditActions.UserObjectType,
                Details = JsonSerializer.Serialize(new Dictionary<string, object?> { [key] = new { path, count = 7 } }),
            });
            await db.SaveChangesAsync();
        }
        using var admin = Admin(log);

        Assert.Equal(0, (await Page(admin, $"q={key}")).GetProperty("totalCount").GetInt32());
        // A nested value, backslashes and all, as typed.
        Assert.Equal(1, (await Page(admin, $"q={Uri.EscapeDataString(path)}")).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task A_save_retried_after_a_failure_writes_one_audit_row_per_lifecycle_row()
    {
        var s = await f.Seed(MeetingStatus.Published);
        await using (var db = f.Db())
        {
            db.MeetingStatusHistory.Add(new MeetingStatusHistory
            {
                MeetingId = s.MeetingId, FromStatus = MeetingStatus.Published, ToStatus = MeetingStatus.Published,
                Action = MeetingHistoryAction.Reschedule, ActorId = s.Organizer, OccurredAt = Base,
            });
            var orphan = new AgendaItem { MeetingId = Guid.NewGuid(), Position = 1, Title = "Không có cuộc họp" };
            db.AgendaItems.Add(orphan);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.Entry(orphan).State = EntityState.Detached;
            await db.SaveChangesAsync();
        }

        await using var check = f.Db();
        Assert.Equal(1, await check.AuditLog.CountAsync(a => a.ObjectId == s.MeetingId && a.Action == "meeting.rescheduled"));
    }

    [Fact]
    public async Task Pages_keep_their_size_and_report_the_total()
    {
        var log = await Seed();
        using var admin = Admin(log);

        var last = await Page(admin, $"q={log.Marker}&pageSize=3&page=3");
        Assert.Equal((3, 3, 8, 3), (last.GetProperty("page").GetInt32(), last.GetProperty("pageSize").GetInt32(),
            last.GetProperty("totalCount").GetInt32(), last.GetProperty("totalPages").GetInt32()));
        Assert.Equal([AuditActions.RoleCreated, AuditActions.UserUpdated], Actions(last));
        var beyond = await Page(admin, $"q={log.Marker}&pageSize=3&page={int.MaxValue}");
        Assert.Empty(beyond.GetProperty("items").EnumerateArray());
        var none = await Page(admin, "q=khong-co-dong-nao-" + Guid.NewGuid().ToString("N"));
        Assert.Equal((0, 0), (none.GetProperty("totalCount").GetInt32(), none.GetProperty("totalPages").GetInt32()));
        Assert.Equal(20, (await Page(admin, $"q={log.Marker}")).GetProperty("pageSize").GetInt32());
    }

    [Theory]
    [InlineData("from=2026-11-02T00:00:00Z&to=2026-11-01T00:00:00Z", "to")]
    [InlineData("from=2026-11-01", "from")]
    [InlineData("actor=ai-do", "actor")]
    [InlineData("action=meeting.deleted", "action")]
    [InlineData("pageSize=101", "pageSize")]
    [InlineData("page=0", "page")]
    [InlineData("sortBy=action", "sortBy")]
    [InlineData("actor=guest&actor=system", "actor")]
    public async Task An_invalid_period_or_filter_is_refused_with_MSG469(string query, string field)
    {
        var log = await Seed();
        using var admin = Admin(log);

        var body = await AssertCode(await admin.GetAsync($"{Url}?{query}"), 400, "MSG469");

        Assert.True(body.GetProperty("errors").TryGetProperty(field, out _), body.ToString());
    }

    [Fact]
    public async Task A_keyword_longer_than_100_characters_is_refused_with_MSG469()
    {
        var log = await Seed();
        using var admin = Admin(log);
        await Page(admin, "q=" + new string('a', 100));
        var body = await AssertCode(await admin.GetAsync($"{Url}?q={new string('a', 101)}"), 400, "MSG469");
        Assert.True(body.GetProperty("errors").TryGetProperty("q", out _));
    }

    [Fact]
    public async Task A_log_that_cannot_be_read_answers_MSG470()
    {
        var log = await Seed();
        using var admin = Admin(log);
        f.Failures.FailAuditRead = true;
        try
        {
            await AssertCode(await admin.GetAsync(Url), 500, "MSG470");
        }
        finally
        {
            f.Failures.FailAuditRead = false;
        }
    }

    [Fact]
    public async Task Event_types_name_every_code_with_a_group()
    {
        var log = await Seed();
        using var admin = Admin(log);
        var types = await admin.GetFromJsonAsync<JsonElement>(Url + "/event-types");
        var signedIn = types.EnumerateArray().Single(t => t.GetProperty("code").GetString() == AuditActions.SignedIn);
        Assert.Equal(("Đăng nhập", "Đăng nhập"), (signedIn.GetProperty("label").GetString(), signedIn.GetProperty("group").GetString()));
        Assert.Equal(AuditEventCatalog.All.Count, types.GetArrayLength());
    }

    [Fact]
    public async Task Each_lifecycle_move_writes_an_audit_row_in_the_same_save_stamped_with_the_caller_ip()
    {
        var s = await f.Seed(MeetingStatus.Published);
        var options = new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseNpgsql(f.Database.DataSource).UseSnakeCaseNamingConvention().Options;
        var history = new MeetingStatusHistory
        {
            MeetingId = s.MeetingId, FromStatus = MeetingStatus.Published, ToStatus = MeetingStatus.Cancelled,
            Action = MeetingHistoryAction.Cancel, ActorId = s.Organizer, Reason = "Chủ toạ bận đột xuất", OccurredAt = Base,
        };
        await using (var db = new TrustCabinetDbContext(options, new FakeCallerContext("198.51.100.4")))
        {
            db.MeetingStatusHistory.Add(history);
            await db.SaveChangesAsync();
            // A second save of the same context writes nothing more.
            await db.SaveChangesAsync();
        }

        await using var check = f.Db();
        var row = await check.AuditLog.SingleAsync(a => a.ObjectId == s.MeetingId && a.Action == "meeting.cancelled");
        Assert.Equal((s.Organizer, AuditActions.MeetingObjectType, "198.51.100.4", Base),
            (row.ActorId!.Value, row.ObjectType, row.IpAddress, row.OccurredAt));
        var details = JsonDocument.Parse(row.Details).RootElement;
        Assert.Equal((history.Id, "Published", "Cancelled", "Chủ toạ bận đột xuất"),
            (details.GetProperty("historyId").GetGuid(), details.GetProperty("fromStatus").GetString(),
                details.GetProperty("toStatus").GetString(), details.GetProperty("reason").GetString()));
    }

    [Fact]
    public async Task The_system_cancelling_an_overdue_meeting_is_recorded_without_an_actor()
    {
        var s = await f.Seed(MeetingStatus.Published);
        await using (var db = f.Db())
        {
            db.MeetingStatusHistory.Add(new MeetingStatusHistory
            {
                MeetingId = s.MeetingId, FromStatus = MeetingStatus.Published, ToStatus = MeetingStatus.Cancelled,
                Action = MeetingHistoryAction.AutoCancel, Reason = "Quá hạn", OccurredAt = Base,
            });
            await db.SaveChangesAsync();
        }

        await using var check = f.Db();
        var row = await check.AuditLog.SingleAsync(a => a.ObjectId == s.MeetingId);
        Assert.Equal(("meeting.auto_cancelled", (Guid?)null, ""), (row.Action, row.ActorId, row.IpAddress));
    }

    [Fact]
    public async Task An_audit_entry_can_be_neither_changed_nor_deleted()
    {
        var log = await Seed();
        await using var db = f.Db();
        var row = await db.AuditLog.FirstAsync(a => a.ActorId == log.User);

        row.Details = "{}";
        Assert.Equal("The audit log is append-only.",
            (await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync())).Message);
        db.ChangeTracker.Clear();
        db.AuditLog.Remove(await db.AuditLog.FirstAsync(a => a.ActorId == log.User));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
}
