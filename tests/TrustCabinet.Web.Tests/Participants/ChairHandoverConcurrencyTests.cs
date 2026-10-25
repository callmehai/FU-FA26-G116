using System.Net;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;
using static TrustCabinet.Web.Tests.Participants.ParticipantEndpointTests;

namespace TrustCabinet.Web.Tests.Participants;

public sealed class ChairHandoverConcurrencyTests(ParticipantFixture f) : IClassFixture<ParticipantFixture>
{
    private static Task<HttpResponseMessage> Post(HttpClient client, ParticipantFixture.Scenario s) => Write(client,
        s.Url + "/participants/chair-handover", HttpMethod.Post, new { newChairParticipantId = s.MemberAssignment, confirmed = true });

    [Fact]
    public async Task Two_posts_with_same_etag_serialize_and_loser_sees_lost_chair_before_stale()
    {
        var s = await f.Seed(); using var first = f.Client(s.Chair); using var second = f.Client(s.Chair);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Queries.BeforeUserLock = async () => { entered.TrySetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(10)); };
        try
        {
            var winner = Post(first, s);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            f.Queries.BeforeMeetingLock = () => { waiting.TrySetResult(); return Task.CompletedTask; };
            var loser = Post(second, s);
            await waiting.Task.WaitAsync(TimeSpan.FromSeconds(10));
            release.TrySetResult();
            await Code(await winner, 200, "MSG257");
            await Code(await loser, 409, "MSG260");
            await Code(await Post(first, s), 409, "MSG260");
        }
        finally { release.TrySetResult(); f.Queries.BeforeUserLock = null; f.Queries.BeforeMeetingLock = null; }
        await AssertOneSwap(s);
    }

    [Theory]
    [InlineData("removed", "MSG259")]
    [InlineData("secretary", "MSG259")]
    [InlineData("unrelated", "MSG275")]
    [InlineData("ended", "MSG260")]
    public async Task Waiting_request_observes_competing_transaction_after_meeting_lock(string change, string expected)
    {
        var s = await f.Seed(); using var chair = f.Client(s.Chair);
        var listing = await chair.GetAsync(s.Url + "/participants/chair-handover-candidates");
        Assert.Equal("\"participants-0\"", listing.Headers.ETag!.ToString());
        await using var db = f.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM meetings WHERE id = {s.MeetingId} FOR UPDATE");
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Queries.BeforeMeetingLock = () => { waiting.TrySetResult(); return Task.CompletedTask; };
        var pending = Post(chair, s);
        try
        {
            await waiting.Task.WaitAsync(TimeSpan.FromSeconds(10));
            switch (change)
            {
                case "removed": await db.MeetingParticipants.Where(p => p.Id == s.MemberAssignment).ExecuteUpdateAsync(set => set.SetProperty(p => p.AssignmentStatus, AssignmentStatus.Removed)
                    .SetProperty(p => p.RemovedAt, f.Clock.Now).SetProperty(p => p.RemovedById, s.Secretary)); break;
                case "secretary": await db.MeetingParticipants.Where(p => p.Id == s.MemberAssignment).ExecuteUpdateAsync(set => set.SetProperty(p => p.Role, MeetingRole.Secretary)); break;
                case "ended": await db.Meetings.Where(m => m.Id == s.MeetingId).ExecuteUpdateAsync(set => set.SetProperty(m => m.Status, MeetingStatus.Ended)); break;
                case "unrelated": db.Add(new MeetingParticipant { MeetingId = s.MeetingId, UserId = s.Outsider }); await db.SaveChangesAsync(); break;
            }
            await db.Meetings.Where(m => m.Id == s.MeetingId).ExecuteUpdateAsync(set => set.SetProperty(m => m.ParticipantsRevision, 1));
            await tx.CommitAsync();
            await Code(await pending, expected == "MSG259" ? 400 : 409, expected);
        }
        finally { f.Queries.BeforeMeetingLock = null; }
        Assert.Equal(MeetingRole.Chair, (await db.MeetingParticipants.FindAsync(s.ChairAssignment))!.Role);
        Assert.False(await db.MeetingInvitationDispatches.AnyAsync(d => d.MeetingId == s.MeetingId));
        Assert.False(await db.AuditLog.AnyAsync(a => a.ObjectId == s.MeetingId));
        Assert.DoesNotContain(f.Changes.Events, e => e.Change.MeetingId == s.MeetingId);
    }

    [Fact]
    public async Task Deactivation_between_snapshot_and_account_lock_is_authoritative_without_updating_users()
    {
        var s = await f.Seed(); using var chair = f.Client(s.Chair);
        f.Queries.BeforeUserLock = async () =>
        {
            await using var concurrent = f.Db();
            await concurrent.Users.Where(u => u.Id == s.Member).ExecuteUpdateAsync(set => set.SetProperty(u => u.IsActive, false));
        };
        f.Queries.RejectUserUpdates = true;
        try { await Code(await Post(chair, s), 400, "MSG259"); }
        finally { f.Queries.BeforeUserLock = null; f.Queries.RejectUserUpdates = false; }
        await using var db = f.Db();
        Assert.False((await db.Users.FindAsync(s.Member))!.IsActive);
        Assert.Equal(MeetingRole.Chair, (await db.MeetingParticipants.FindAsync(s.ChairAssignment))!.Role);
        Assert.Equal(0, (await db.Meetings.FindAsync(s.MeetingId))!.ParticipantsRevision);
        Assert.False(await db.MeetingInvitationDispatches.AnyAsync(d => d.MeetingId == s.MeetingId));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Post_and_legacy_put_share_the_meeting_lock(bool postFirst)
    {
        var s = await f.Seed(); using var client = f.Client(s.Chair); using var concurrent = f.Client(s.Chair);
        Task<HttpResponseMessage> Put(HttpClient c) => Write(c, s.Url + "/participants/assignments", HttpMethod.Put, new
        {
            changes = new[] { new { participantId = s.ChairAssignment, role = "Member" }, new { participantId = s.MemberAssignment, role = "Chair" } },
            chairReplacement = new { outgoingParticipantId = s.ChairAssignment, incomingParticipantId = s.MemberAssignment, confirmed = true }
        });
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Queries.BeforeUserLock = async () => { entered.TrySetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(10)); };
        try
        {
            var winner = postFirst ? Post(client, s) : Put(client);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            f.Queries.BeforeMeetingLock = () => { waiting.TrySetResult(); return Task.CompletedTask; };
            var loser = postFirst ? Put(concurrent) : Post(concurrent, s);
            await waiting.Task.WaitAsync(TimeSpan.FromSeconds(10));
            release.TrySetResult();
            await Code(await winner, 200, "MSG257");
            await Code(await loser, postFirst ? 403 : 409, postFirst ? "MSG85" : "MSG260");
        }
        finally { release.TrySetResult(); f.Queries.BeforeUserLock = null; f.Queries.BeforeMeetingLock = null; }
        await AssertOneSwap(s);
    }

    [Fact]
    public async Task Secretary_inactive_chair_replacement_remains_put_only()
    {
        var s = await f.Seed(); await using var db = f.Db(); using var secretary = f.Client(s.Secretary);
        await db.Users.Where(u => u.Id == s.Chair).ExecuteUpdateAsync(set => set.SetProperty(u => u.IsActive, false));
        await Code(await Post(secretary, s), 409, "MSG260");
        f.Queries.RejectUserUpdates = true;
        try
        {
            await Code(await Write(secretary, s.Url + "/participants/assignments", HttpMethod.Put, new
            {
                changes = new[] { new { participantId = s.ChairAssignment, role = "Member" }, new { participantId = s.MemberAssignment, role = "Chair" } },
                chairReplacement = new { outgoingParticipantId = s.ChairAssignment, incomingParticipantId = s.MemberAssignment, confirmed = true }
            }), 200, "MSG100");
        }
        finally { f.Queries.RejectUserUpdates = false; }
        // The Secretary acted, so the locked-out Chair and the new one are both told in-app.
        await AssertOneSwap(s, notifications: 2);
    }

    private async Task AssertOneSwap(ParticipantFixture.Scenario s, int notifications = 1)
    {
        await using var db = f.Db();
        Assert.Equal(1, (await db.Meetings.FindAsync(s.MeetingId))!.ParticipantsRevision);
        Assert.Equal(1, await db.MeetingParticipants.CountAsync(p => p.MeetingId == s.MeetingId && p.Role == MeetingRole.Chair));
        Assert.Equal(1, await db.MeetingParticipants.CountAsync(p => p.MeetingId == s.MeetingId && p.Role == MeetingRole.Secretary));
        // By default the outgoing Chair handed over and gets only the email, not an in-app notice.
        Assert.Equal(notifications, await db.Notifications.CountAsync(n => n.MeetingId == s.MeetingId));
        Assert.Equal(2, await db.MeetingInvitationDispatches.CountAsync(d => d.MeetingId == s.MeetingId));
        Assert.Equal(1, await db.AuditLog.CountAsync(a => a.ObjectId == s.MeetingId));
        Assert.Single(f.Changes.Events, e => e.Change.MeetingId == s.MeetingId);
    }
}
