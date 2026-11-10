using TrustCabinet.Domain.CalendarSharing;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Web.Tests.CalendarSharing;

/// <summary>BR-419/BR-420 — who and which meetings make someone busy, and the busy periods a viewer may see.</summary>
public sealed class CalendarBusyRulesTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 9, 2, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(MeetingStatus.Draft, false)]
    [InlineData(MeetingStatus.WaitingApproval, true)]
    [InlineData(MeetingStatus.Rejected, false)]
    [InlineData(MeetingStatus.Approved, true)]
    [InlineData(MeetingStatus.Published, true)]
    [InlineData(MeetingStatus.InProgress, true)]
    [InlineData(MeetingStatus.Ended, true)]
    [InlineData(MeetingStatus.Cancelled, false)]
    public void Only_meetings_on_their_way_or_held_count_as_busy(MeetingStatus status, bool busy)
        => Assert.Equal(busy, CalendarBusyRules.CountsAsBusy(status));

    [Theory]
    [InlineData(MeetingRole.Chair, InvitationResponse.Pending, true)]
    [InlineData(MeetingRole.Chair, InvitationResponse.Declined, true)] // the Chair is never asked
    [InlineData(MeetingRole.Secretary, InvitationResponse.Accepted, true)]
    [InlineData(MeetingRole.Member, InvitationResponse.Pending, true)]
    [InlineData(MeetingRole.Member, InvitationResponse.Declined, false)]
    [InlineData(MeetingRole.Member, InvitationResponse.Delegated, false)]
    [InlineData(MeetingRole.Guest, InvitationResponse.Accepted, false)]
    public void Only_a_current_chair_secretary_or_member_who_did_not_decline_is_busy(MeetingRole role,
        InvitationResponse response, bool busy)
        => Assert.Equal(busy, CalendarBusyRules.IsBusyAssignment(AssignmentStatus.Current, role, response, null));

    [Fact]
    public void A_removed_assignment_is_not_busy()
        => Assert.False(CalendarBusyRules.IsBusyAssignment(AssignmentStatus.Removed, MeetingRole.Member,
            InvitationResponse.Accepted, null));

    [Fact]
    public void The_busy_period_follows_what_actually_happened()
    {
        var planned = (Nine, Nine.AddHours(1));
        Assert.Equal(new BusyInterval(Nine, Nine.AddHours(1)), CalendarBusyRules.Interval(MeetingStatus.Published,
            planned.Item1, planned.Item2, null, null, Nine));
        // Ended: until it really ended.
        Assert.Equal(new BusyInterval(Nine.AddMinutes(5), Nine.AddMinutes(30)), CalendarBusyRules.Interval(
            MeetingStatus.Ended, planned.Item1, planned.Item2, Nine.AddMinutes(5), Nine.AddMinutes(30), Nine));
        // Running past its planned end: at least until now.
        Assert.Equal(new BusyInterval(Nine, Nine.AddHours(2)), CalendarBusyRules.Interval(MeetingStatus.InProgress,
            planned.Item1, planned.Item2, Nine, null, Nine.AddHours(2)));
        Assert.Null(CalendarBusyRules.Interval(MeetingStatus.Cancelled, planned.Item1, planned.Item2, null, null, Nine));
    }

    [Fact]
    public void Overlapping_and_touching_periods_merge_so_the_count_of_meetings_does_not_show()
    {
        var merged = CalendarBusyRules.Merge(
        [
            new(Nine.AddHours(3), Nine.AddHours(4)),
            new(Nine, Nine.AddHours(1)),
            new(Nine.AddMinutes(30), Nine.AddMinutes(90)),
            new(Nine.AddMinutes(90), Nine.AddHours(2)),
        ]);
        Assert.Equal([new(Nine, Nine.AddHours(2)), new(Nine.AddHours(3), Nine.AddHours(4))], merged);
    }

    [Fact]
    public void Clipping_keeps_only_the_part_inside_the_window()
    {
        var clipped = CalendarBusyRules.Clip([new(Nine.AddHours(-2), Nine.AddHours(1)), new(Nine.AddHours(5), Nine.AddHours(6))],
            Nine, Nine.AddHours(5));
        Assert.Equal([new BusyInterval(Nine, Nine.AddHours(1))], clipped);
    }
}
