using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure.Agenda;
using TrustCabinet.Infrastructure.Seed;
using static TrustCabinet.Web.Tests.Agenda.AgendaEndpointTests;

namespace TrustCabinet.Web.Tests.Agenda;

public sealed class AgendaAdditionalTests(AgendaFixture fixture) : IClassFixture<AgendaFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Publish_timeout_drains_scoped_work_and_observes_late_failure(bool failAfterCancellation)
    {
        var s = await fixture.Seed(count: 0);
        await using var scope = fixture.Host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrustCabinet.Infrastructure.TrustCabinetDbContext>();
        var publisher = new DelayedPublisher(db, failAfterCancellation);
        var service = ActivatorUtilities.CreateInstance<AgendaService>(scope.ServiceProvider, publisher);
        var mutation = service.CreateAsync(s.MeetingId, s.Secretary, new("Committed", PlannedDurationMinutes: 20));
        try
        {
            await publisher.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.ThrowsAsync<TimeoutException>(() => mutation.WaitAsync(TimeSpan.FromMilliseconds(200)));
            Assert.False(mutation.IsCompleted);
            Assert.False(publisher.Finished);
            await using var check = fixture.Db();
            Assert.True(await check.AgendaItems.AnyAsync(i => i.MeetingId == s.MeetingId));
        }
        finally
        {
            publisher.Release.TrySetResult();
            await mutation.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.True(publisher.Finished);
        Assert.True(publisher.ReadAfterCancellation);
        Assert.Equal(AgendaFailure.None, (await mutation).Failure);
    }

    private sealed class DelayedPublisher(TrustCabinet.Infrastructure.TrustCabinetDbContext db, bool fail) : IAgendaChangePublisher
    {
        internal readonly TaskCompletionSource CancellationObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Finished;
        internal bool ReadAfterCancellation;

        public async Task PublishAsync(AgendaChange change, CancellationToken ct)
        {
            using var registration = ct.Register(() => CancellationObserved.TrySetResult());
            try
            {
                // Simulate a provider finishing scoped work only after cancellation has been requested.
                await Release.Task;
                ReadAfterCancellation = await db.AgendaItems.AnyAsync(i => i.MeetingId == change.MeetingId);
                if (fail) throw new IOException("Injected late publisher failure.");
            }
            finally { Finished = true; }
        }
    }

    [Fact]
    public async Task Seeder_is_insert_only_and_survives_user_edits_and_deleted_items()
    {
        await using var db = fixture.Db();
        foreach (var email in new[] { "thuky", "chutoa", "thanhvien" })
            db.Users.Add(new() { Email = email + "@trustcabinet.local", FullName = email, PasswordHash = "test-only" });
        await db.SaveChangesAsync();
        await AgendaDevelopmentDataSeeder.SeedAsync(db);
        var meeting = await db.Meetings.SingleAsync(m => m.Id == AgendaDevelopmentDataSeeder.RunningMeetingId);
        meeting.Title = "User edit";
        var item = await db.AgendaItems.FirstAsync(i => i.MeetingId == meeting.Id && i.Position == 4);
        db.Remove(item);
        await db.SaveChangesAsync();
        await AgendaDevelopmentDataSeeder.SeedAsync(db);
        Assert.Equal("User edit", (await db.Meetings.SingleAsync(m => m.Id == meeting.Id)).Title);
        Assert.Equal(2, await db.AgendaItems.CountAsync(i => i.MeetingId == meeting.Id));
        Assert.Equal(6, await db.MeetingDocuments.CountAsync(d => d.FileName.EndsWith("(fixture)")));
    }

    [Fact]
    public async Task System_admin_presenter_and_other_meeting_secretary_do_not_gain_roles()
    {
        var s = await fixture.Seed();
        await using (var db = fixture.Db())
        {
            (await db.Users.SingleAsync(u => u.Id == s.Member)).RoleId = BuiltInRoles.Admin;
            (await db.AgendaItems.SingleAsync(i => i.Id == s.Items[0])).PresenterId = s.Member;
            await db.SaveChangesAsync();
        }
        using var member = fixture.Client(s.Member);
        await AssertCode(await member.PostAsJsonAsync(s.Url, Input()), 403, "MSG67");
        await AssertCode(await member.PostAsync(s.Url + $"/{s.Items[0]}/current", null), 403, "MSG73");
        var other = await fixture.Seed();
        using var otherSecretary = fixture.Client(other.Secretary);
        Assert.Equal(HttpStatusCode.NotFound, (await otherSecretary.GetAsync(s.Url)).StatusCode);
    }

    [Fact]
    public async Task Reorder_keeps_done_and_current_positions_noop_keeps_updated_at()
    {
        var s = await fixture.Seed(count: 4);
        await using (var db = fixture.Db())
        {
            var first = await db.AgendaItems.SingleAsync(i => i.Id == s.Items[0]);
            first.Status = AgendaItemStatus.Done;
            first.ActualStartedAt = fixture.Clock.Now.AddMinutes(-2);
            first.ActualEndedAt = fixture.Clock.Now.AddMinutes(-1);
            var current = await db.AgendaItems.SingleAsync(i => i.Id == s.Items[2]);
            current.Status = AgendaItemStatus.InProgress;
            current.ActualStartedAt = fixture.Clock.Now;
            await db.SaveChangesAsync();
        }
        using var client = fixture.Client(s.Secretary);
        var snapshot = await client.GetFromJsonAsync<JsonElement>(s.Url);
        await AssertCode(await client.PutAsJsonAsync(s.Url + "/order", new { orderVersion = snapshot.GetProperty("orderVersion").GetString(), pendingItemIds = new[] { s.Items[3], s.Items[1] } }), 200, "MSG63");
        await using var check = fixture.Db();
        var ordered = await check.AgendaItems.Where(i => i.MeetingId == s.MeetingId).OrderBy(i => i.Position).ToArrayAsync();
        Assert.Equal(new[] { s.Items[0], s.Items[3], s.Items[2], s.Items[1] }, ordered.Select(i => i.Id));
        Assert.Equal(new[] { 1, 4, 7, 10 }, ordered.Select(i => i.Position));
        Assert.Equal(fixture.Clock.Now, ordered[2].ActualStartedAt);
        var before = ordered.Select(i => i.UpdatedAt).ToArray();
        var events = fixture.Events.Count;
        snapshot = await client.GetFromJsonAsync<JsonElement>(s.Url);
        await AssertCode(await client.PutAsJsonAsync(s.Url + "/order", new { orderVersion = snapshot.GetProperty("orderVersion").GetString(), pendingItemIds = new[] { s.Items[3], s.Items[1] } }), 200, "MSG63");
        check.ChangeTracker.Clear();
        Assert.Equal(before, await check.AgendaItems.Where(i => i.MeetingId == s.MeetingId).OrderBy(i => i.Position).Select(i => i.UpdatedAt).ToArrayAsync());
        Assert.Equal(events, fixture.Events.Count);
    }

    [Fact]
    public async Task Removing_selection_does_not_delete_documents_and_completed_presenter_can_be_inactive()
    {
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Secretary);
        using var chair = fixture.Client(s.Chair);
        await AssertCode(await client.PutAsJsonAsync(s.Url + $"/{s.Items[0]}", Input(presenter: s.Member, documents: [s.DocumentId])), 200, "MSG61");
        await AssertCode(await client.PutAsJsonAsync(s.Url + $"/{s.Items[0]}", Input(presenter: s.Member)), 200, "MSG61");
        await AssertCode(await chair.PostAsync(s.Url + $"/{s.Items[0]}/current", null), 200, "MSG64");
        await AssertCode(await chair.PostAsync(s.Url + $"/{s.Items[0]}/complete", null), 200, "MSG65");
        await using (var db = fixture.Db())
        {
            (await db.Users.SingleAsync(u => u.Id == s.Member)).IsActive = false;
            await db.SaveChangesAsync();
            Assert.Null(await AgendaFixture.LinkedItem(db, s.DocumentId));
        }
        await AssertCode(await client.PutAsJsonAsync(s.Url + $"/{s.Items[0]}", Input("New title", presenter: s.Member)), 200, "MSG61");
    }

    [Fact]
    public async Task Delete_constraint_failure_preserves_notes_and_agenda()
    {
        var s = await fixture.Seed();
        await using (var db = fixture.Db())
        {
            db.MeetingNotes.AddRange(new MeetingNote { MeetingId = s.MeetingId, AgendaItemId = s.Items[0], AuthorId = s.Secretary },
                new MeetingNote { MeetingId = s.MeetingId, AuthorId = s.Secretary });
            await db.SaveChangesAsync();
        }
        using var client = fixture.Client(s.Secretary);
        await AssertCode(await client.DeleteAsync(s.Url + $"/{s.Items[0]}"), 503, "MSG70");
        await using var check = fixture.Db();
        Assert.Equal(2, await check.MeetingNotes.CountAsync(n => n.MeetingId == s.MeetingId));
        Assert.True(await check.AgendaItems.AnyAsync(i => i.Id == s.Items[0]));
    }

    [Fact]
    public async Task Invalid_actual_time_fails_without_changing_evidence_and_helper_no_current_is_noop()
    {
        var s = await fixture.Seed();
        await using (var db = fixture.Db())
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            Assert.Null(await new AgendaMeetingEnd(db).PrepareAsync(s.MeetingId, fixture.Clock.Now));
            await transaction.CommitAsync();
            var item = await db.AgendaItems.SingleAsync(i => i.Id == s.Items[0]);
            item.Status = AgendaItemStatus.InProgress;
            await db.SaveChangesAsync();
        }
        using var chair = fixture.Client(s.Chair);
        await AssertCode(await chair.PostAsync(s.Url + $"/{s.Items[0]}/complete", null), 503, "MSG76");
        await using var check = fixture.Db();
        Assert.Null((await check.AgendaItems.SingleAsync(i => i.Id == s.Items[0])).ActualEndedAt);
    }

    [Fact]
    public async Task Database_failure_linking_multiple_documents_rolls_back_the_inserted_item()
    {
        var s = await fixture.Seed();
        Guid failingDocument;
        await using (var db = fixture.Db())
        {
            var document = AgendaFixture.TestDocument(s.MeetingId, "Injected link failure", s.Secretary);
            db.MeetingDocuments.Add(document);
            await db.SaveChangesAsync();
            failingDocument = document.Id;
            await db.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION agenda_test_fail_link() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM meeting_documents WHERE id = NEW.meeting_document_id
                        AND file_name = 'Injected link failure') THEN
                        RAISE EXCEPTION 'Injected association persistence failure';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER agenda_test_link_failure BEFORE INSERT ON meeting_document_agenda_items
                    FOR EACH ROW EXECUTE FUNCTION agenda_test_fail_link();
                """);
        }
        var events = fixture.Events.Count;
        try
        {
            using var client = fixture.Client(s.Secretary);
            await AssertCode(await client.PostAsJsonAsync(s.Url, Input(documents: [s.DocumentId, failingDocument])), 503, "MSG68");
            await using var check = fixture.Db();
            Assert.Equal(3, await check.AgendaItems.CountAsync(i => i.MeetingId == s.MeetingId));
            Assert.False(await check.MeetingDocumentAgendaItems.AnyAsync(l => l.MeetingDocument.MeetingId == s.MeetingId));
            Assert.Equal(events, fixture.Events.Count);
        }
        finally
        {
            await using var cleanup = fixture.Db();
            await cleanup.Database.ExecuteSqlRawAsync("DROP TRIGGER agenda_test_link_failure ON meeting_document_agenda_items; DROP FUNCTION agenda_test_fail_link();");
        }
    }

    [Fact]
    public async Task End_helper_and_meeting_history_rollback_when_caller_save_fails()
    {
        var s = await fixture.Seed();
        using var chair = fixture.Client(s.Chair);
        await AssertCode(await chair.PostAsync(s.Url + $"/{s.Items[0]}/current", null), 200, "MSG64");
        await using (var db = fixture.Db())
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var change = await new AgendaMeetingEnd(db).PrepareAsync(s.MeetingId, fixture.Clock.Now.AddMinutes(1));
            Assert.NotNull(change);
            var meeting = await db.Meetings.SingleAsync(m => m.Id == s.MeetingId);
            meeting.Status = MeetingStatus.Ended;
            meeting.ActualEndedAt = change.OccurredAt;
            meeting.Title = new string('x', 301);
            db.MeetingStatusHistory.Add(new() { MeetingId = s.MeetingId, ActorId = s.Chair,
                FromStatus = MeetingStatus.InProgress, ToStatus = MeetingStatus.Ended,
                Action = MeetingHistoryAction.LegacyTransition, OccurredAt = change.OccurredAt });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            await transaction.RollbackAsync();
        }
        await using var check = fixture.Db();
        Assert.Equal(MeetingStatus.InProgress, (await check.Meetings.SingleAsync(m => m.Id == s.MeetingId)).Status);
        Assert.Equal(AgendaItemStatus.InProgress, (await check.AgendaItems.SingleAsync(i => i.Id == s.Items[0])).Status);
        Assert.Empty(await check.MeetingStatusHistory.Where(h => h.MeetingId == s.MeetingId).ToArrayAsync());
    }

    [Fact]
    public async Task Cancellation_propagates_instead_of_becoming_business_failure()
    {
        var s = await fixture.Seed();
        await using var scope = fixture.Host.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IAgendaService>();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetAsync(s.MeetingId, s.Secretary, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CreateAsync(s.MeetingId, s.Secretary, new("X", PlannedDurationMinutes: 20), cancelled.Token));
    }
}
