using System.Text.Json;
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
        public Exception? ProbeFailure;
        public Exception? PrepareFailure;
        public Task<bool> IsAvailableAsync(CancellationToken ct) => ProbeFailure is not null
            ? Task.FromException<bool>(ProbeFailure) : Task.FromResult(Result.Status != GuestLinkStatus.Unavailable);
        public GuestInvitationLink Result = new(GuestLinkStatus.Unavailable);
        public Task<GuestInvitationLink> PrepareAsync(Guid invitationId, CancellationToken ct) => PrepareFailure is not null
            ? Task.FromException<GuestInvitationLink>(PrepareFailure) : Task.FromResult(Result);
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
    public async Task Hybrid_meeting_without_a_room_is_placed_at_its_link_alone()
    {
        var s = await f.Seed();
        await using (var db = f.Db())
            await db.Meetings.Where(m => m.Id == s.MeetingId).ExecuteUpdateAsync(set => set
                .SetProperty(m => m.Mode, MeetingMode.Hybrid).SetProperty(m => m.Location, ""));
        using var secretary = f.Client(s.Secretary);
        await Code(await Write(secretary, s.Url + "/participants", HttpMethod.Post,
            new { userIds = new[] { s.Outsider }, role = "Member" }, await Tag(secretary, s.Url)), 201, "MSG96");
        await using var read = f.Db();
        var payload = await read.MeetingInvitationDispatches.Where(d => d.MeetingId == s.MeetingId)
            .Select(d => d.PayloadJson).SingleAsync();
        Assert.Equal("Phòng họp trực tuyến TrustCabinet", JsonDocument.Parse(payload).RootElement.GetProperty("Place").GetString());
    }

    [Fact]
    public async Task Unavailable_guest_uses_exponential_dependency_delay_without_locking_meeting()
    {
        var s = await f.Seed();
        var id = await AddGuest(s);
        var originalTime = f.Clock.Now;
        await using var locked = f.Db();
        await using var tx = await locked.Database.BeginTransactionAsync();
        await locked.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM meetings WHERE id = {s.MeetingId} FOR UPDATE");
        try
        {
            foreach (var minutes in new[] { 1, 5, 30, 360, 360 })
            {
                var started = f.Clock.Now;
                Assert.Equal(0, await Process(id).WaitAsync(TimeSpan.FromSeconds(5)));
                await using var verify = f.Db();
                var dispatch = await verify.MeetingInvitationDispatches.SingleAsync(d => d.Id == id);
                Assert.Equal(InvitationDispatchState.BlockedDependency, dispatch.State);
                Assert.Equal(started.AddMinutes(minutes), dispatch.NextAttemptAt);
                Assert.Equal(0, dispatch.AttemptCount);
                f.Clock.Now = dispatch.NextAttemptAt!.Value;
            }
        }
        finally { f.Clock.Now = originalTime; }
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Removal_requires_materialized_invitation_for_same_owner(bool guest)
    {
        var s = await f.Seed();
        using var secretary = f.Client(s.Secretary);
        Guid id;
        Guid ownerId;
        if (guest)
        {
            id = await AddGuest(s);
            Assert.Equal(0, await Process(id)); // UC-04 unavailable.
            await using var db = f.Db();
            ownerId = await db.MeetingGuestInvitations.Where(g => g.MeetingId == s.MeetingId).Select(g => g.Id).SingleAsync();
        }
        else
        {
            await Code(await Write(secretary, s.Url + "/participants", HttpMethod.Post,
                new { userIds = new[] { s.Outsider }, role = "Member" }), 201, "MSG96");
            await using var db = f.Db();
            var invitation = await db.MeetingInvitationDispatches.SingleAsync(d => d.MeetingId == s.MeetingId);
            id = invitation.Id;
            ownerId = invitation.ParticipantId!.Value;
        }
        var route = guest ? "guest-invitations" : "participants";
        await Code(await Write(secretary, s.Url + $"/{route}/{ownerId}", HttpMethod.Delete,
            etag: await Tag(secretary, s.Url)), 200, "MSG103");
        await using var check = f.Db();
        var removed = await check.MeetingInvitationDispatches.SingleAsync(d => d.MeetingId == s.MeetingId && d.EventKind == InvitationEventKind.Removed);
        Assert.Equal(0, await Process(removed.Id)); // Also safe before processing the original invitation.
        Assert.Equal(InvitationDispatchState.Suppressed,
            (await check.MeetingInvitationDispatches.AsNoTracking().SingleAsync(d => d.Id == removed.Id)).State);
        Assert.Empty(await check.EmailMessages.Where(e => e.MeetingId == s.MeetingId).ToArrayAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Materialized_guest_invitation_allows_removal_without_protected_link(bool legacyEmailOnly)
    {
        var s = await f.Seed(); var id = await AddGuest(s);
        Assert.Equal(1, await Process(id, new Links { Result = new(GuestLinkStatus.Ready, "https://meetings.test/verify") }));
        await using var db = f.Db();
        var guest = await db.MeetingGuestInvitations.SingleAsync(g => g.MeetingId == s.MeetingId);
        if (legacyEmailOnly)
            await db.MeetingInvitationDispatches.Where(d => d.Id == id).ExecuteDeleteAsync();
        using var secretary = f.Client(s.Secretary);
        await Code(await Write(secretary, s.Url + $"/guest-invitations/{guest.Id}", HttpMethod.Delete,
            etag: await Tag(secretary, s.Url)), 200, "MSG103");
        var removed = await db.MeetingInvitationDispatches.SingleAsync(d => d.MeetingId == s.MeetingId && d.EventKind == InvitationEventKind.Removed);
        Assert.Equal(1, await Process(removed.Id));
        var email = await db.EmailMessages.SingleAsync(e => e.DeduplicationKey == removed.DeduplicationKey);
        Assert.DoesNotContain("https://", email.Body); Assert.Null(email.CalendarAttachment);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task Unexpected_provider_errors_retry_and_exhaust_budget(bool probe, bool cancellationException)
    {
        var s = await f.Seed(); var id = await AddGuest(s);
        Exception failure = cancellationException ? new OperationCanceledException("credential-must-not-leak")
            : new InvalidOperationException("credential-must-not-leak");
        var links = new Links { Result = new(GuestLinkStatus.Ready, "https://meetings.test/verify"),
            ProbeFailure = probe ? failure : null, PrepareFailure = probe ? null : failure };
        await using var db = f.Db();
        for (var attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            Assert.Equal(0, await Process(id, links));
            var dispatch = await db.MeetingInvitationDispatches.SingleAsync(d => d.Id == id);
            await db.Entry(dispatch).ReloadAsync();
            Assert.Equal(attempt, dispatch.AttemptCount);
            Assert.Equal(failure.GetType().Name, dispatch.LastError);
            Assert.True(dispatch.NextAttemptAt > f.Clock.Now);
            Assert.Equal(attempt == _options.MaxAttempts ? InvitationDispatchState.Failed : InvitationDispatchState.Pending, dispatch.State);
            if (attempt < _options.MaxAttempts) { dispatch.NextAttemptAt = null; await db.SaveChangesAsync(); }
        }
    }

    [Fact]
    public async Task Unexpected_dispatch_failure_does_not_block_later_dispatches_in_batch()
    {
        var s = await f.Seed(); var id = await AddGuest(s);
        using var secretary = f.Client(s.Secretary);
        await Code(await Write(secretary, s.Url + "/participants", HttpMethod.Post,
            new { userIds = new[] { s.Outsider }, role = "Member" }, await Tag(secretary, s.Url)), 201, "MSG96");
        await using var db = f.Db();
        var processor = new MeetingInvitationDispatchProcessor(db, Options.Create(_options), f.Clock,
            new Links { ProbeFailure = new InvalidOperationException("private-provider-detail") },
            NullLogger<MeetingInvitationDispatchProcessor>.Instance);
        await processor.ProcessNextAsync(default);
        Assert.Equal(1, await db.MeetingInvitationDispatches.Where(d => d.Id == id).Select(d => d.AttemptCount).SingleAsync());
        Assert.Single(await db.EmailMessages.Where(e => e.MeetingId == s.MeetingId).ToArrayAsync());
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
    public async Task Publication_stages_all_recipients_in_the_same_transaction()
    {
        var s = await f.Seed(MeetingStatus.Approved); using var secretary = f.Client(s.Secretary);
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
            Assert.DoesNotContain("Permissions", events.Single(d => d.ParticipantId == s.MemberAssignment).PayloadJson, StringComparison.Ordinal);
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
