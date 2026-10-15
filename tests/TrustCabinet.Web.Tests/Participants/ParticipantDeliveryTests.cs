using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Infrastructure.Email;
using TrustCabinet.Infrastructure.Meetings;
using TrustCabinet.Infrastructure.Participants;
using static TrustCabinet.Web.Tests.Participants.ParticipantEndpointTests;

namespace TrustCabinet.Web.Tests.Participants;

public sealed class ParticipantDeliveryTests(ParticipantFixture f) : IClassFixture<ParticipantFixture>
{
    private readonly EmailDeliveryOptions _options = new()
    {
        ApplicationBaseUrl = "https://meetings.test",
        MaxAttempts = 2,
        Smtp = new() { Enabled = true, FromAddress = "no-reply@meetings.test", FromName = "TrustCabinet" }
    };
    private sealed class Links : IGuestInvitationLinkProvider
    {
        public GuestInvitationLink Result = new(GuestLinkStatus.Unavailable);
        public Task<GuestInvitationLink> PrepareAsync(Guid invitationId, CancellationToken ct) => Task.FromResult(Result);
    }
    private async Task<int> Process(Guid id, Links? links = null)
    {
        await using var db = f.Db();
        return await new MeetingInvitationDispatchProcessor(db, Options.Create(_options), f.Clock, links ?? new(),
            NullLogger<MeetingInvitationDispatchProcessor>.Instance).ProcessAsync(id, default);
    }
    private async Task<Guid> AddGuest(ParticipantFixture.Scenario s)
    {
        using var secretary = f.Client(s.Secretary);
        await Code(await Write(secretary, s.Url + "/guest-invitations", HttpMethod.Post,
            new { invitedName = "Khách", email = $"{Guid.NewGuid():N}@guests.test" }), 201, "MSG96");
        await using var db = f.Db();
        return await db.MeetingInvitationDispatches.Where(d => d.MeetingId == s.MeetingId).Select(d => d.Id).SingleAsync();
    }

    [Fact]
    public async Task Materialization_is_atomic_deduplicated_and_smtp_disabled_retains_intent()
    {
        var s = await f.Seed(); using var secretary = f.Client(s.Secretary);
        await Code(await Write(secretary, s.Url + "/participants", HttpMethod.Post,
            new { userIds = new[] { s.Outsider }, role = "Member" }), 201, "MSG96");
        await using var db = f.Db();
        var intent = await db.MeetingInvitationDispatches.AsNoTracking().SingleAsync(d => d.MeetingId == s.MeetingId);
        _options.Smtp.Enabled = false;
        Assert.Equal(0, await Process(intent.Id));
        Assert.Empty(await db.EmailMessages.Where(e => e.MeetingId == s.MeetingId).ToArrayAsync());
        _options.Smtp.Enabled = true;
        Assert.Equal(1, (await Task.WhenAll(Process(intent.Id), Process(intent.Id))).Sum());
        var email = await db.EmailMessages.SingleAsync(e => e.MeetingId == s.MeetingId);
        Assert.Equal(intent.DeduplicationKey, email.DeduplicationKey);
        Assert.Contains("Thành viên", email.Body); Assert.Contains("BEGIN:VCALENDAR", email.CalendarAttachment);
        Assert.Equal(0, await Process(intent.Id));
        Assert.Single(await db.EmailMessages.Where(e => e.MeetingId == s.MeetingId).ToArrayAsync());
    }

    [Fact]
    public async Task Guest_dependency_blocks_without_consuming_retry_then_verified_link_can_materialize()
    {
        var s = await f.Seed(); var id = await AddGuest(s); var links = new Links();
        Assert.Equal(0, await Process(id, links));
        await using var db = f.Db();
        var dispatch = await db.MeetingInvitationDispatches.SingleAsync(d => d.Id == id);
        Assert.Equal(InvitationDispatchState.BlockedDependency, dispatch.State); Assert.Equal(0, dispatch.AttemptCount);
        dispatch.NextAttemptAt = null; await db.SaveChangesAsync();
        links.Result = new(GuestLinkStatus.Ready, "https://meetings.test/guest/verify?invitation=opaque-test-value");
        Assert.Equal(1, await Process(id, links));
        var email = await db.EmailMessages.SingleAsync(e => e.MeetingId == s.MeetingId);
        Assert.Contains("Xác minh email", email.Body); Assert.DoesNotContain("/mgmt/meetings", email.Body);
        Assert.DoesNotContain("Địa điểm:", email.Body); Assert.Contains("/guest/verify", email.CalendarAttachment);
    }

    [Fact]
    public async Task Revocation_suppresses_pending_guest_invitation_and_removal_has_no_protected_link()
    {
        var s = await f.Seed(); var id = await AddGuest(s);
        await using var db = f.Db(); var guest = await db.MeetingGuestInvitations.SingleAsync(g => g.MeetingId == s.MeetingId);
        using var secretary = f.Client(s.Secretary);
        await Code(await Write(secretary, s.Url + $"/guest-invitations/{guest.Id}", HttpMethod.Delete, etag: await Tag(secretary, s.Url)), 200, "MSG103");
        Assert.Equal(0, await Process(id));
        var removed = await db.MeetingInvitationDispatches.SingleAsync(d => d.MeetingId == s.MeetingId && d.EventKind == InvitationEventKind.Removed);
        Assert.Equal(1, await Process(removed.Id));
        var email = await db.EmailMessages.SingleAsync(e => e.MeetingId == s.MeetingId);
        Assert.DoesNotContain("https://", email.Body); Assert.Null(email.CalendarAttachment);
        Assert.Equal(InvitationDispatchState.Suppressed, (await db.MeetingInvitationDispatches.AsNoTracking().SingleAsync(d => d.Id == id)).State);
    }

    [Fact]
    public async Task Invalid_provider_url_retries_without_exposing_url_and_exhausts_attempt_budget()
    {
        var s = await f.Seed(); var id = await AddGuest(s);
        var links = new Links { Result = new(GuestLinkStatus.Ready, "https://attacker.test/secret-token") };
        Assert.Equal(0, await Process(id, links));
        await using var db = f.Db();
        var dispatch = await db.MeetingInvitationDispatches.SingleAsync(d => d.Id == id);
        Assert.Equal(1, dispatch.AttemptCount); Assert.Equal("JsonException", dispatch.LastError);
        dispatch.NextAttemptAt = null; await db.SaveChangesAsync();
        Assert.Equal(0, await Process(id, links));
        await db.Entry(dispatch).ReloadAsync(); Assert.Equal(InvitationDispatchState.Failed, dispatch.State);
        Assert.Empty(await db.EmailMessages.Where(e => e.MeetingId == s.MeetingId).ToArrayAsync());
    }

    [Fact]
    public async Task Publication_stages_all_recipients_and_grants_in_the_same_transaction()
    {
        var s = await f.Seed(MeetingStatus.Approved); using var secretary = f.Client(s.Secretary);
        await Code(await Write(secretary, s.Url + "/participants/assignments", HttpMethod.Put, new
        {
            changes = new[] {
            new { participantId = s.MemberAssignment, role = "Member", additionalPermissions = new[] { "ViewInvitationResponses" } } }
        }), 200, "MSG153");
        await Code(await Write(secretary, s.Url + "/guest-invitations", HttpMethod.Post,
            new { invitedName = "Guest", email = $"{Guid.NewGuid():N}@guests.test" }, await Tag(secretary, s.Url)), 201, "MSG96");
        await using (var db = f.Db())
        {
            Assert.Empty(await db.MeetingInvitationDispatches.Where(d => d.MeetingId == s.MeetingId).ToArrayAsync());
            await using var tx = await db.Database.BeginTransactionAsync();
            var repo = new MeetingRepository(db); var meeting = (await repo.FindWorkflowAsync(s.MeetingId, true))!;
            meeting.Status = MeetingStatus.Published;
            await repo.StagePublicationInvitationsAsync(meeting, f.Clock.Now); await db.SaveChangesAsync();
            var events = await db.MeetingInvitationDispatches.Where(d => d.MeetingId == s.MeetingId).ToArrayAsync();
            Assert.Equal(4, events.Length);
            Assert.Contains("ViewInvitationResponses", events.Single(d => d.ParticipantId == s.MemberAssignment).PayloadJson);
            await tx.RollbackAsync();
        }
        await using var check = f.Db();
        Assert.Equal(MeetingStatus.Approved, (await check.Meetings.FindAsync(s.MeetingId))!.Status);
        Assert.Empty(await check.MeetingInvitationDispatches.Where(d => d.MeetingId == s.MeetingId).ToArrayAsync());
    }

    [Fact]
    public async Task Reschedule_notifies_current_guests_atomically_and_deduplicates_each_event()
    {
        var s = await f.Seed(); var initialId = await AddGuest(s);
        var links = new Links { Result = new(GuestLinkStatus.Ready, "https://meetings.test/guest/verify?key=test-only") };
        Assert.Equal(1, await Process(initialId, links));
        await using var db = f.Db();
        var guest = await db.MeetingGuestInvitations.SingleAsync(g => g.MeetingId == s.MeetingId);
        db.MeetingGuestInvitations.Add(new()
        {
            MeetingId = s.MeetingId, InvitedName = "Revoked guest", Email = $"{Guid.NewGuid():N}@guests.test",
            InvitedById = s.Secretary, RevokedAt = f.Clock.Now, RevokedById = s.Secretary,
            InitialInvitationDeliveryKey = $"guest:{Guid.NewGuid()}:invited"
        });
        await db.SaveChangesAsync();
        var meeting = (await db.Meetings.FindAsync(s.MeetingId))!;
        var input = new MeetingInput(meeting.Title, f.Clock.Now.AddDays(1).ToString("O"), f.Clock.Now.AddDays(1).AddHours(1).ToString("O"),
            meeting.Mode.ToString(), "Phòng mới", meeting.OnlineLink, meeting.Sensitivity.ToString(), meeting.Description);
        async Task<MeetingResult> Reschedule(MeetingInput value)
        {
            await using var scope = f.Host.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IMeetingService>().UpdateAsync(s.MeetingId, value, s.Organizer);
        }
        f.Failure.MeetingId = s.MeetingId;
        try { Assert.Equal(MeetingOutcome.SaveFailed, (await Reschedule(input)).Outcome); }
        finally { f.Failure.Reset(); }
        Assert.Equal(meeting.PlannedStartTime, await db.Meetings.AsNoTracking().Where(m => m.Id == s.MeetingId).Select(m => m.PlannedStartTime).SingleAsync());
        Assert.Empty(await db.MeetingStatusHistory.Where(h => h.MeetingId == s.MeetingId).ToArrayAsync());
        Assert.Empty(await db.Notifications.Where(n => n.MeetingId == s.MeetingId).ToArrayAsync());
        Assert.Single(await db.MeetingInvitationDispatches.Where(d => d.MeetingId == s.MeetingId).ToArrayAsync());
        Assert.Equal(MeetingOutcome.Success, (await Reschedule(input)).Outcome);
        var history = await db.MeetingStatusHistory.SingleAsync(h => h.MeetingId == s.MeetingId);
        var dispatch = Assert.Single(await db.MeetingInvitationDispatches.Where(d => d.MeetingId == s.MeetingId && d.Id != initialId).ToArrayAsync());
        Assert.Equal(guest.Id, dispatch.GuestInvitationId);
        Assert.Equal(history.Id, dispatch.EventId);
        Assert.Contains(history.Id.ToString(), dispatch.DeduplicationKey);
        Assert.Equal(3, await db.Notifications.CountAsync(n => n.MeetingId == s.MeetingId && n.Type == NotificationType.MeetingRescheduled));
        Assert.Equal(MeetingOutcome.Success, (await Reschedule(input)).Outcome);
        Assert.Equal(2, await db.MeetingInvitationDispatches.CountAsync(d => d.MeetingId == s.MeetingId));
        Assert.Equal(1, (await Task.WhenAll(Process(dispatch.Id, links), Process(dispatch.Id, links))).Sum());
        var email = await db.EmailMessages.SingleAsync(e => e.DeduplicationKey == dispatch.DeduplicationKey);
        Assert.Contains("dời lịch", email.Subject);
        Assert.Contains("15/10/2026", email.Body);
        Assert.DoesNotContain("Phòng mới", email.Body);
        Assert.Equal(guest.Email, email.ToAddress);
        Assert.Equal(MeetingOutcome.Success, (await Reschedule(input with { PlannedStartTime = f.Clock.Now.AddDays(2).ToString("O"), PlannedEndTime = f.Clock.Now.AddDays(2).AddHours(1).ToString("O") })).Outcome);
        Assert.Equal(3, await db.MeetingInvitationDispatches.CountAsync(d => d.MeetingId == s.MeetingId));
    }

    [Fact]
    public async Task Production_defaults_never_issue_guest_access_or_credentials()
    {
        Assert.False(await new UnavailableParticipantGuestAccess().CanAccessAsync(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), default));
        Assert.Equal(GuestLinkStatus.Unavailable, (await new UnavailableGuestInvitationLinkProvider().PrepareAsync(Guid.NewGuid(), default)).Status);
    }

    [Fact]
    public async Task Failure_saving_materialized_email_rolls_back_and_retry_creates_only_one_message()
    {
        var s = await f.Seed(); var id = await AddGuest(s);
        var links = new Links { Result = new(GuestLinkStatus.Ready, "https://meetings.test/guest/verify?key=test-only") };
        await using (var scope = f.Host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Infrastructure.TrustCabinetDbContext>();
            f.Failure.MeetingId = s.MeetingId;
            try
            {
                Assert.Equal(0, await new MeetingInvitationDispatchProcessor(db, Options.Create(_options), f.Clock, links,
                    NullLogger<MeetingInvitationDispatchProcessor>.Instance).ProcessAsync(id, default));
            }
            finally { f.Failure.Reset(); }
        }
        await using var check = f.Db();
        Assert.Empty(await check.EmailMessages.Where(e => e.MeetingId == s.MeetingId).ToArrayAsync());
        var dispatch = await check.MeetingInvitationDispatches.SingleAsync(d => d.Id == id);
        Assert.Equal(InvitationDispatchState.Pending, dispatch.State); Assert.Null(dispatch.EmailMessageId); Assert.Equal(1, dispatch.AttemptCount);
        dispatch.NextAttemptAt = null; await check.SaveChangesAsync();
        Assert.Equal(1, await Process(id, links)); Assert.Equal(0, await Process(id, links));
        Assert.Single(await check.EmailMessages.Where(e => e.MeetingId == s.MeetingId).ToArrayAsync());
    }
}
