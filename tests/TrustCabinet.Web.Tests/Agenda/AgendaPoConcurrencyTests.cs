using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;
using static TrustCabinet.Web.Tests.Agenda.AgendaEndpointTests;

namespace TrustCabinet.Web.Tests.Agenda;

public sealed class AgendaPoConcurrencyTests(AgendaFixture fixture) : IClassFixture<AgendaFixture>
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Removal_clears_all_presenters_atomically_or_rolls_back(bool guest, bool fail)
    {
        var s = await fixture.Seed();
        var invitation = AgendaPoRequirementsTests.Guest(s);
        Guid target;
        long revision;
        await using (var db = fixture.Db())
        {
            db.Add(invitation);
            target = guest ? invitation.Id : (await db.MeetingParticipants.SingleAsync(p => p.MeetingId == s.MeetingId && p.UserId == s.Member)).Id;
            revision = (await db.Meetings.SingleAsync(m => m.Id == s.MeetingId)).ParticipantsRevision;
            var items = await db.AgendaItems.Where(i => i.MeetingId == s.MeetingId).OrderBy(i => i.Position).ToArrayAsync();
            for (var i = 0; i < items.Length; i++)
            {
                items[i].Status = (AgendaItemStatus)i;
                items[i].PresenterId = guest ? null : s.Member;
                items[i].PresenterGuestInvitationId = guest ? invitation.Id : null;
                items[i].LegacyPresenterName = "Historical";
                items[i].Conclusion = "Keep conclusion";
                items[i].ConclusionUpdatedById = s.Secretary;
                items[i].ConclusionUpdatedAt = fixture.Clock.Now;
                items[i].ActualStartedAt = i == 0 ? null : fixture.Clock.Now.AddMinutes(-1);
                items[i].ActualEndedAt = i == 2 ? fixture.Clock.Now : null;
            }
            db.MeetingDocumentAgendaItems.Add(new() { MeetingDocumentId = s.DocumentId, AgendaItemId = s.Items[0] });
            await db.SaveChangesAsync();
        }
        await using var scope = fixture.Host.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IParticipantService>();
        fixture.SaveFailure.Enabled = fail;
        ParticipantResult result;
        try { result = await service.MutateAsync(s.MeetingId, target, new(s.Secretary), guest ? ParticipantOperation.RevokeGuest : ParticipantOperation.RemoveInternal, new(Revision: revision), default); }
        finally { fixture.SaveFailure.Enabled = false; }
        Assert.Equal(fail ? ParticipantFailure.SaveFailed : ParticipantFailure.None, result.Failure);
        await using var check = fixture.Db();
        var after = await check.AgendaItems.Where(i => i.MeetingId == s.MeetingId).OrderBy(i => i.Position).ToArrayAsync();
        for (var i = 0; i < after.Length; i++)
        {
            Assert.Equal((AgendaItemStatus)i, after[i].Status);
            Assert.Equal(1 + i * 3, after[i].Position);
            Assert.Equal("Keep conclusion", after[i].Conclusion);
            Assert.Equal(s.Secretary, after[i].ConclusionUpdatedById);
            Assert.Equal(i == 0 ? (DateTimeOffset?)null : fixture.Clock.Now.AddMinutes(-1), after[i].ActualStartedAt);
            Assert.Equal(i == 2 ? (DateTimeOffset?)fixture.Clock.Now : null, after[i].ActualEndedAt);
            Assert.Equal(fail ? "Historical" : "", after[i].LegacyPresenterName);
            Assert.Equal(fail ? (guest ? invitation.Id : s.Member) : (Guid?)null, guest ? after[i].PresenterGuestInvitationId : after[i].PresenterId);
        }
        Assert.Equal(s.Items[0], await AgendaFixture.LinkedItem(check, s.DocumentId));
        Assert.Equal(fail ? 0 : 1, fixture.Events.Count(e => e.MeetingId == s.MeetingId && e.ChangeType == "PresenterCleared"));
        if (guest) Assert.Equal(!fail, (await check.MeetingGuestInvitations.SingleAsync(g => g.Id == target)).RevokedAt.HasValue);
        else Assert.Equal(fail ? AssignmentStatus.Current : AssignmentStatus.Removed, (await check.MeetingParticipants.SingleAsync(p => p.Id == target)).AssignmentStatus);
    }

    [Theory]
    [InlineData("minutes")]
    [InlineData("role")]
    [InlineData("remove")]
    public async Task Conclusion_rechecks_minutes_and_role_after_waiting_for_meeting_lock(string mutation)
    {
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Secretary);
        await using var locked = fixture.Db();
        var item = await locked.AgendaItems.SingleAsync(i => i.Id == s.Items[0]);
        item.Status = AgendaItemStatus.Done;
        await locked.SaveChangesAsync();
        await using var tx = await locked.Database.BeginTransactionAsync();
        await locked.Meetings.FromSqlInterpolated($"SELECT * FROM meetings WHERE id = {s.MeetingId} FOR UPDATE").ToListAsync();
        var request = client.PutAsJsonAsync(s.Url + $"/{s.Items[0]}/conclusion", new { conclusion = "Should not save" });
        await Waiter();
        if (mutation == "minutes") locked.MeetingMinutes.Add(new() { MeetingId = s.MeetingId, CreatedById = s.Secretary, Content = "" });
        else
        {
            var participant = await locked.MeetingParticipants.SingleAsync(p => p.MeetingId == s.MeetingId && p.UserId == s.Secretary);
            if (mutation == "role") participant.Role = MeetingRole.Member;
            else { participant.AssignmentStatus = AssignmentStatus.Removed; participant.RemovedAt = fixture.Clock.Now; participant.RemovedById = s.Chair; }
        }
        await locked.SaveChangesAsync();
        await tx.CommitAsync();
        await AssertCode(await request.WaitAsync(TimeSpan.FromSeconds(10)), mutation == "minutes" ? 409 : mutation == "role" ? 403 : 404,
            mutation == "remove" ? "MSG174" : "MSG273");
        await using var check = fixture.Db();
        Assert.Null((await check.AgendaItems.SingleAsync(i => i.Id == s.Items[0])).Conclusion);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Presenter_assignment_rechecks_removal_or_revocation_after_lock(bool guest)
    {
        var s = await fixture.Seed();
        var invitation = AgendaPoRequirementsTests.Guest(s);
        await using var locked = fixture.Db();
        locked.Add(invitation); await locked.SaveChangesAsync();
        await using var tx = await locked.Database.BeginTransactionAsync();
        await locked.Meetings.FromSqlInterpolated($"SELECT * FROM meetings WHERE id = {s.MeetingId} FOR UPDATE").ToListAsync();
        using var client = fixture.Client(s.Secretary);
        var request = client.PutAsJsonAsync(s.Url + $"/{s.Items[0]}", new
        {
            title = "Assign", presenterId = guest ? (Guid?)null : s.Member,
            presenterGuestInvitationId = guest ? invitation.Id : (Guid?)null
        });
        await Waiter();
        if (guest) { invitation.RevokedAt = fixture.Clock.Now; invitation.RevokedById = s.Secretary; }
        else
        {
            var participant = await locked.MeetingParticipants.SingleAsync(p => p.MeetingId == s.MeetingId && p.UserId == s.Member);
            participant.AssignmentStatus = AssignmentStatus.Removed;
            participant.RemovedAt = fixture.Clock.Now;
            participant.RemovedById = s.Secretary;
        }
        await locked.SaveChangesAsync(); await tx.CommitAsync();
        await AssertCode(await request.WaitAsync(TimeSpan.FromSeconds(10)), 400, "MSG66");
        await using var check = fixture.Db();
        var item = await check.AgendaItems.SingleAsync(i => i.Id == s.Items[0]);
        Assert.Null(item.PresenterId); Assert.Null(item.PresenterGuestInvitationId);
    }

    [Fact]
    public async Task Concurrent_conclusions_store_one_whole_content_author_time_tuple()
    {
        var s = await fixture.Seed();
        await using (var db = fixture.Db())
        {
            (await db.AgendaItems.SingleAsync(i => i.Id == s.Items[0])).Status = AgendaItemStatus.Done;
            await db.SaveChangesAsync();
        }
        using var chair = fixture.Client(s.Chair);
        using var secretary = fixture.Client(s.Secretary);
        var url = s.Url + $"/{s.Items[0]}/conclusion";
        var responses = await Task.WhenAll(chair.PutAsJsonAsync(url, new { conclusion = "Chair" }), secretary.PutAsJsonAsync(url, new { conclusion = "Secretary" }));
        foreach (var response in responses) await AssertCode(response, 200, "MSG271");
        await using var check = fixture.Db();
        var item = await check.AgendaItems.SingleAsync(i => i.Id == s.Items[0]);
        Assert.Equal(item.Conclusion == "Chair" ? s.Chair : s.Secretary, item.ConclusionUpdatedById);
        Assert.Equal(fixture.Clock.Now, item.ConclusionUpdatedAt);
        Assert.Equal(2, fixture.Events.Count(e => e.MeetingId == s.MeetingId && e.ChangeType == "ConclusionUpdated"));
    }

    private async Task Waiter()
    {
        await using var observer = fixture.Db();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            var count = await observer.Database.SqlQueryRaw<int>("""
                SELECT count(*)::int AS "Value" FROM pg_stat_activity
                WHERE datname = current_database() AND wait_event_type = 'Lock' AND query LIKE '%FOR UPDATE%'
                """).SingleAsync(timeout.Token);
            if (count > 0) return;
            await Task.Delay(10, timeout.Token);
        }
    }
}
