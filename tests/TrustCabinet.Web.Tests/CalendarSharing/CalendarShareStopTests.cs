using System.Net.Http.Json;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using static TrustCabinet.Web.Tests.CalendarSharing.CalendarSharingFixture;

namespace TrustCabinet.Web.Tests.CalendarSharing;

/// <summary>
/// BR-424 through the real administration endpoints: deactivating an account (UC-06c), changing a person's role (UC-08) and
/// taking MeetingCreator off a role (UC-11) stop the affected Effective shares at once, for good, telling both sides.
/// A Pending invitation is kept (PO 09/11/2026, Q-C).
/// </summary>
public sealed class CalendarShareStopTests(CalendarSharingFixture f) : IClassFixture<CalendarSharingFixture>
{
    private HttpClient Admin(People p) => f.Client(p.Admin, nameof(Capability.AdminUsers));

    private static void AssertStoppedNotice(List<Notification> notices)
        => Assert.Single(notices, n => n.Type == NotificationType.CalendarShareStopped);

    [Fact]
    public async Task Deactivating_the_owner_or_the_viewer_stops_effective_shares_keeps_pending_and_restarts_nothing()
    {
        var p = await f.Seed();
        var asOwner = await f.AddShare(p.Owner, p.Organizer);
        var pending = await f.AddShare(p.Owner, p.Organizer2, CalendarShareStatus.Pending);
        var unrelated = await f.AddShare(p.Owner2, p.Organizer);
        using var admin = Admin(p);

        await AssertCode(await admin.PutAsJsonAsync($"/api/users/{p.Owner}/status", new { isActive = false }), 200, "MSG11");

        Assert.Equal(CalendarShareStatus.Stopped, (await f.Share(asOwner)).Status);
        Assert.Equal(CalendarShareStatus.Pending, (await f.Share(pending)).Status);
        Assert.Equal(CalendarShareStatus.Effective, (await f.Share(unrelated)).Status);
        AssertStoppedNotice(await f.NotificationsOf(p.Owner));
        AssertStoppedNotice(await f.NotificationsOf(p.Organizer));
        Assert.Empty(await f.NotificationsOf(p.Organizer2));
        var audit = Assert.Single(await f.AuditOf(asOwner));
        Assert.Equal(AuditActions.CalendarShareStopped, audit.Action);
        Assert.Equal(p.Admin, audit.ActorId);
        Assert.Contains("accountDeactivated", audit.Details);

        await AssertCode(await admin.PutAsJsonAsync($"/api/users/{p.Owner}/status", new { isActive = true }), 200, "MSG11");
        Assert.Equal(CalendarShareStatus.Stopped, (await f.Share(asOwner)).Status);

        // The viewer side too.
        await AssertCode(await admin.PutAsJsonAsync($"/api/users/{p.Organizer}/status", new { isActive = false }), 200, "MSG11");
        Assert.Equal(CalendarShareStatus.Stopped, (await f.Share(unrelated)).Status);
    }

    [Fact]
    public async Task A_share_past_its_end_date_is_left_alone_when_an_account_is_deactivated_or_loses_the_right()
    {
        var p = await f.Seed();
        // Ended on 31/10 and still Effective, since nothing writes the lapse (BR-425, PO 09/11/2026 Q-B).
        var lapsedOwned = await f.AddShare(p.Owner, p.Organizer, start: Today.AddDays(-20), end: new DateOnly(2026, 10, 31));
        var lapsedViewed = await f.AddShare(p.Owner2, p.Organizer2, start: Today.AddDays(-20), end: Today.AddDays(-1));
        // The last day is today: still in force, so it does stop.
        var lastDay = await f.AddShare(p.Owner, p.Organizer2, start: Today.AddDays(-5), end: Today);
        using var admin = Admin(p);

        await AssertCode(await admin.PutAsJsonAsync($"/api/users/{p.Owner}/status", new { isActive = false }), 200, "MSG11");
        await using (var db = f.Db())
        {
            var organizer2 = await db.Users.FindAsync(p.Organizer2);
            await AssertCode(await admin.PutAsJsonAsync($"/api/users/{p.Organizer2}", new
            {
                fullName = organizer2!.FullName, email = organizer2.Email, roleId = BuiltInRoles.Member.ToString(),
            }), 200, "MSG10");
        }

        foreach (var id in new[] { lapsedOwned, lapsedViewed })
        {
            Assert.Equal(CalendarShareStatus.Effective, (await f.Share(id)).Status);
            Assert.Empty(await f.AuditOf(id));
        }

        Assert.Equal(CalendarShareStatus.Stopped, (await f.Share(lastDay)).Status);
        // Only the share still in force is announced: one notice to each of its two people, none about the lapsed ones.
        Assert.DoesNotContain(await f.NotificationsOf(p.Organizer), n => n.Type == NotificationType.CalendarShareStopped);
        Assert.DoesNotContain(await f.NotificationsOf(p.Owner2), n => n.Type == NotificationType.CalendarShareStopped);
        AssertStoppedNotice(await f.NotificationsOf(p.Owner));
        AssertStoppedNotice(await f.NotificationsOf(p.Organizer2));
    }

    [Fact]
    public async Task A_new_role_without_meeting_creator_stops_the_shares_the_person_views_but_not_their_own()
    {
        var p = await f.Seed();
        var viewing = await f.AddShare(p.Owner, p.Organizer);
        var owning = await f.AddShare(p.Organizer, p.Organizer2);
        using var admin = Admin(p);
        await using var db = f.Db();
        var organizer = await db.Users.FindAsync(p.Organizer);

        // Secretary → Chair keeps MeetingCreator: nothing stops.
        await AssertCode(await admin.PutAsJsonAsync($"/api/users/{p.Organizer}", new
        {
            fullName = organizer!.FullName, email = organizer.Email, roleId = BuiltInRoles.Chair.ToString(),
        }), 200, "MSG10");
        Assert.Equal(CalendarShareStatus.Effective, (await f.Share(viewing)).Status);

        // Chair → Member loses it: what they view stops, what they own does not.
        await AssertCode(await admin.PutAsJsonAsync($"/api/users/{p.Organizer}", new
        {
            fullName = organizer.FullName, email = organizer.Email, roleId = BuiltInRoles.Member.ToString(),
        }), 200, "MSG10");
        Assert.Equal(CalendarShareStatus.Stopped, (await f.Share(viewing)).Status);
        Assert.Equal(CalendarShareStatus.Effective, (await f.Share(owning)).Status);
        Assert.Contains("viewerLostMeetingCreator", Assert.Single(await f.AuditOf(viewing)).Details);
        AssertStoppedNotice(await f.NotificationsOf(p.Owner));

        // Getting the right back restarts nothing.
        await AssertCode(await admin.PutAsJsonAsync($"/api/users/{p.Organizer}", new
        {
            fullName = organizer.FullName, email = organizer.Email, roleId = BuiltInRoles.Secretary.ToString(),
        }), 200, "MSG10");
        Assert.Equal(CalendarShareStatus.Stopped, (await f.Share(viewing)).Status);
    }

    [Fact]
    public async Task Taking_meeting_creator_off_a_role_stops_the_shares_its_holders_view()
    {
        var p = await f.Seed();
        // A role of its own, so the built-in matrix the other tests rely on stays untouched.
        var role = new Role { Name = $"Ban tổ chức {Guid.NewGuid():N}"[..30] };
        await using (var db = f.Db())
        {
            db.Roles.Add(role);
            db.RoleCapabilities.Add(new RoleCapability { RoleId = role.Id, Capability = Capability.MeetingCreator });
            (await db.Users.FindAsync(p.Owner2))!.RoleId = role.Id;
            await db.SaveChangesAsync();
        }

        var viewing = await f.AddShare(p.Owner, p.Owner2);
        using var admin = Admin(p);
        // Unchanged rights stop nothing.
        await AssertCode(await admin.PutAsJsonAsync("/api/roles/capabilities", new
        {
            roleId = role.Id.ToString(), capabilities = new[] { "MeetingCreator", "MinutesEditor" },
        }), 200, "MSG16");
        Assert.Equal(CalendarShareStatus.Effective, (await f.Share(viewing)).Status);

        await AssertCode(await admin.PutAsJsonAsync("/api/roles/capabilities", new
        {
            roleId = role.Id.ToString(), capabilities = new[] { "MinutesEditor" },
        }), 200, "MSG16");
        Assert.Equal(CalendarShareStatus.Stopped, (await f.Share(viewing)).Status);
        AssertStoppedNotice(await f.NotificationsOf(p.Owner));
        AssertStoppedNotice(await f.NotificationsOf(p.Owner2));

        // A stopped share blocks nothing once the right is back: viewing again is a new share.
        await AssertCode(await admin.PutAsJsonAsync("/api/roles/capabilities", new
        {
            roleId = role.Id.ToString(), capabilities = new[] { "MeetingCreator" },
        }), 200, "MSG16");
        Assert.Equal(CalendarShareStatus.Stopped, (await f.Share(viewing)).Status);
        using var viewer = f.Client(p.Owner2);
        await AssertCode(await Request(viewer, p.Owner, "Level1", Today, Today.AddDays(5)), 201, "MSG435");
    }
}
