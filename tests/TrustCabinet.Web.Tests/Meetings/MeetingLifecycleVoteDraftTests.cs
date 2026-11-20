using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Application.Meetings;
using TrustCabinet.Application.Retention;

namespace TrustCabinet.Web.Tests.Meetings;

/// <summary>Ending a meeting deletes the vote drafts it still has, with one audit row each (PO 08/11/2026, SCRUM-28).</summary>
public sealed partial class MeetingLifecycleTests
{
    private async Task<Guid> SeedVote(Guid meetingId, Guid createdBy, VotingStatus status, string title)
    {
        await using var db = fixture.Db();
        var now = fixture.Clock.Now;
        var vote = new VotingSession
        {
            MeetingId = meetingId, Title = title, Description = "Mô tả " + title, Status = status, IsAnonymous = true,
            EligibleRoles = [MeetingRole.Member, MeetingRole.Guest], CreatedById = createdBy,
            OpenedAt = status == VotingStatus.Draft ? null : now.AddMinutes(-10),
            ClosedAt = status == VotingStatus.Closed ? now.AddMinutes(-5) : null,
            CreatedAt = now.AddMinutes(-20), UpdatedAt = now.AddMinutes(-20),
            Options = [new() { Position = 1, Label = "Đồng ý" }, new() { Position = 2, Label = "Không đồng ý" }]
        };
        db.VotingSessions.Add(vote);
        await db.SaveChangesAsync();
        return vote.Id;
    }

    [Fact]
    public async Task Ending_the_meeting_deletes_its_unopened_drafts_and_audits_each_one()
    {
        var s = await fixture.Seed(MeetingStatus.InProgress);
        var other = await fixture.Seed(MeetingStatus.InProgress);
        var draft = await SeedVote(s.MeetingId, s.Secretary, VotingStatus.Draft, "Nháp chưa mở");
        var secondDraft = await SeedVote(s.MeetingId, s.Chair, VotingStatus.Draft, "Nháp thứ hai");
        var open = await SeedVote(s.MeetingId, s.Chair, VotingStatus.Open, "Đang mở");
        var closed = await SeedVote(s.MeetingId, s.Chair, VotingStatus.Closed, "Đã đóng");
        var otherDraft = await SeedVote(other.MeetingId, other.Secretary, VotingStatus.Draft, "Nháp cuộc họp khác");

        await Expect(await Act(s, "End"), 200, "MSG111");

        await using var check = fixture.Db();
        var left = await check.VotingSessions.Where(v => v.MeetingId == s.MeetingId).Select(v => v.Id).ToArrayAsync();
        // An Open or Closed vote is evidence and stays (BR-189); only drafts go, with their options.
        Assert.Equal(new[] { open, closed }.Order(), left.Order());
        Assert.False(await check.VotingOptions.AnyAsync(o => o.VotingSessionId == draft || o.VotingSessionId == secondDraft));
        Assert.True(await check.VotingSessions.AnyAsync(v => v.Id == otherDraft));

        var audit = await check.AuditLog.Where(a => a.Action == AuditActions.VoteDraftDeletedOnMeetingEnd
            && (a.ObjectId == draft || a.ObjectId == secondDraft || a.ObjectId == otherDraft)).ToArrayAsync();
        Assert.Equal(new[] { draft, secondDraft }.Order(), audit.Select(a => a.ObjectId!.Value).Order());
        var entry = audit.Single(a => a.ObjectId == draft);
        Assert.Null(entry.ActorId);
        Assert.Equal(AuditActions.VoteObjectType, entry.ObjectType);
        Assert.Equal(fixture.Clock.Now, entry.OccurredAt);
        var details = JsonDocument.Parse(entry.Details).RootElement;
        Assert.Equal(s.MeetingId, details.GetProperty("meetingId").GetGuid());
        Assert.Equal(s.Chair, details.GetProperty("endedById").GetGuid());
        Assert.Equal("Nháp chưa mở", details.GetProperty("title").GetString());
        Assert.Equal(s.Secretary, details.GetProperty("createdById").GetGuid());
        Assert.Equal(new[] { "Đồng ý", "Không đồng ý" },
            details.GetProperty("options").EnumerateArray().Select(o => o.GetString()!).ToArray());
        Assert.Equal(new[] { "Member", "Guest" },
            details.GetProperty("eligibleRoles").EnumerateArray().Select(r => r.GetString()!).ToArray());
    }

    [Fact]
    public async Task A_failed_end_keeps_the_drafts_and_writes_no_audit_row()
    {
        var s = await fixture.Seed(MeetingStatus.InProgress);
        var draft = await SeedVote(s.MeetingId, s.Secretary, VotingStatus.Draft, "Nháp giữ lại");
        try
        {
            fixture.SaveFailure.Enabled = true;
            await Expect(await Act(s, "End"), 503, "MSG116");
        }
        finally { fixture.SaveFailure.Enabled = false; }

        await using var check = fixture.Db();
        Assert.Equal(VotingStatus.Draft, (await check.VotingSessions.SingleAsync(v => v.Id == draft)).Status);
        Assert.False(await check.AuditLog.AnyAsync(a => a.ObjectId == draft));
        Assert.Equal(MeetingStatus.InProgress, (await check.Meetings.SingleAsync(m => m.Id == s.MeetingId)).Status);
    }

    /// <summary>The organizer, with the capability to create (and so cancel) meetings.</summary>
    private async Task GrantCreator(Guid user)
    {
        await using var db = fixture.Db();
        var role = new Role { Name = "Creator-" + Guid.NewGuid() };
        db.Roles.Add(role);
        db.RoleCapabilities.Add(new RoleCapability { RoleId = role.Id, Capability = Capability.MeetingCreator });
        await db.SaveChangesAsync();
        await db.Users.Where(u => u.Id == user).ExecuteUpdateAsync(u => u.SetProperty(x => x.RoleId, role.Id));
    }

    private async Task<(Guid[] Left, AuditLogEntry[] Audit)> DraftsAfter(Guid meetingId, params Guid[] drafts)
    {
        await using var check = fixture.Db();
        var left = await check.VotingSessions.Where(v => v.MeetingId == meetingId).Select(v => v.Id).ToArrayAsync();
        var audit = await check.AuditLog.AsNoTracking()
            .Where(a => a.Action == AuditActions.VoteDraftDeletedOnMeetingCancel && drafts.Contains(a.ObjectId!.Value)).ToArrayAsync();
        return (left, audit);
    }

    [Fact]
    public async Task Cancelling_the_meeting_deletes_its_drafts_and_audits_each_one()
    {
        var s = await fixture.Seed(MeetingStatus.Published);
        await GrantCreator(s.Organizer);
        var draft = await SeedVote(s.MeetingId, s.Secretary, VotingStatus.Draft, "Nháp cuộc họp bị huỷ");

        var response = await fixture.Client(s.Organizer).PostAsJsonAsync(Url(s.MeetingId), new { action = "Cancel", reason = "Đổi kế hoạch" });
        await Expect(response, 200, "MSG261");

        var (left, audit) = await DraftsAfter(s.MeetingId, draft);
        Assert.Empty(left);
        var entry = Assert.Single(audit);
        Assert.Null(entry.ActorId);
        var details = JsonDocument.Parse(entry.Details).RootElement;
        Assert.Equal(s.MeetingId, details.GetProperty("meetingId").GetGuid());
        Assert.Equal(s.Organizer, details.GetProperty("cancelledById").GetGuid());
        Assert.Equal("meeting_cancelled", details.GetProperty("reason").GetString());
        Assert.Equal("Nháp cuộc họp bị huỷ", details.GetProperty("title").GetString());
    }

    [Fact]
    public async Task The_systems_cancel_deletes_the_drafts_too()
    {
        var s = await fixture.Seed(MeetingStatus.Published);
        await using (var db = fixture.Db())
            await db.Meetings.Where(m => m.Id == s.MeetingId).ExecuteUpdateAsync(u => u
                .SetProperty(m => m.PlannedStartTime, fixture.Clock.Now.AddHours(-30))
                .SetProperty(m => m.PlannedEndTime, fixture.Clock.Now.AddHours(-29)));
        var draft = await SeedVote(s.MeetingId, s.Secretary, VotingStatus.Draft, "Nháp tự huỷ");

        using (var scope = fixture.Host.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IMeetingOverdueService>().CancelOverdueAsync();

        var (left, audit) = await DraftsAfter(s.MeetingId, draft);
        Assert.Empty(left);
        var details = JsonDocument.Parse(Assert.Single(audit).Details).RootElement;
        Assert.Equal(JsonValueKind.Null, details.GetProperty("cancelledById").ValueKind);
    }

    [Fact]
    public async Task The_retention_job_deletes_drafts_left_in_an_already_cancelled_meeting()
    {
        var s = await fixture.Seed(MeetingStatus.Cancelled);
        var other = await fixture.Seed(MeetingStatus.Published);
        var draft = await SeedVote(s.MeetingId, s.Secretary, VotingStatus.Draft, "Nháp còn sót");
        var closed = await SeedVote(s.MeetingId, s.Chair, VotingStatus.Closed, "Đã đóng");
        var kept = await SeedVote(other.MeetingId, other.Secretary, VotingStatus.Draft, "Nháp cuộc họp còn mở");

        using (var scope = fixture.Host.Services.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<IDataRetentionService>().DeleteCancelledMeetingDraftsAsync() >= 1);

        var (left, audit) = await DraftsAfter(s.MeetingId, draft);
        Assert.Equal(new[] { closed }, left);
        Assert.Single(audit);
        await using var check = fixture.Db();
        Assert.True(await check.VotingSessions.AnyAsync(v => v.Id == kept));
    }
}
