using TrustCabinet.Domain.CalendarSharing;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Web.Tests.CalendarSharing;

/// <summary>SCRUM-142 rules that need no database: BR-414/417 levels, BR-431 ranges, BR-425 end date, who may do what.</summary>
public sealed class CalendarShareRulesTests
{
    private static readonly DateOnly Today = new(2026, 11, 8);
    private static readonly Guid OwnerId = Guid.CreateVersion7();
    private static readonly Guid ViewerId = Guid.CreateVersion7();

    private static CalendarShare Share(CalendarShareStatus status = CalendarShareStatus.Effective,
        CalendarShareLevel level = CalendarShareLevel.Level1, int startOffset = 0, int endOffset = 30,
        CalendarShareInitiator initiatedBy = CalendarShareInitiator.Viewer, bool pendingChange = false)
        => new()
        {
            OwnerId = OwnerId, ViewerId = ViewerId, Status = status, Level = level, InitiatedBy = initiatedBy,
            StartDate = Today.AddDays(startOffset), EndDate = Today.AddDays(endOffset),
            PendingLevel = pendingChange ? CalendarShareLevel.Level2 : null,
            PendingEndDate = pendingChange ? Today.AddDays(60) : null,
            PendingRequestedAt = pendingChange ? DateTimeOffset.UnixEpoch : null,
        };

    [Theory]
    [InlineData(null, Sensitivity.Normal, true)]
    [InlineData(null, Sensitivity.Internal, true)]
    [InlineData(null, Sensitivity.Secret, true)]
    [InlineData(CalendarShareLevel.Level1, Sensitivity.Normal, true)]
    [InlineData(CalendarShareLevel.Level1, Sensitivity.Internal, false)]
    [InlineData(CalendarShareLevel.Level1, Sensitivity.Secret, false)]
    [InlineData(CalendarShareLevel.Level2, Sensitivity.Normal, true)]
    [InlineData(CalendarShareLevel.Level2, Sensitivity.Internal, true)]
    [InlineData(CalendarShareLevel.Level2, Sensitivity.Secret, false)]
    public void A_level_shows_only_its_sensitivities_and_never_secret(CalendarShareLevel? level, Sensitivity sensitivity,
        bool shown)
        => Assert.Equal(shown, CalendarShareRules.Covers(level, sensitivity));

    [Theory]
    [InlineData("Level1", true)]
    [InlineData("Level2", true)]
    [InlineData("level1", false)]
    [InlineData("Level3", false)]
    [InlineData("0", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_the_two_level_names_parse(string? value, bool parses)
        => Assert.Equal(parses, CalendarShareRules.TryParseLevel(value, out _));

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(0, 364, true)] // 365 days counting both ends
    [InlineData(0, 365, false)] // 366 days
    [InlineData(-1, 10, false)] // starts in the past
    [InlineData(5, 4, false)] // start after end
    [InlineData(10, 374, true)]
    public void A_new_share_starts_today_or_later_and_covers_at_most_365_days(int start, int end, bool valid)
        => Assert.Equal(valid, CalendarShareRules.ValidNewRange(Today.AddDays(start), Today.AddDays(end), Today));

    [Fact]
    public void An_owner_accepting_a_request_may_only_narrow_it()
    {
        var share = Share(CalendarShareStatus.Pending, CalendarShareLevel.Level2, 0, 30);
        Assert.True(CalendarShareRules.ValidNarrowing(share, CalendarShareLevel.Level1, Today.AddDays(2), Today.AddDays(10), Today));
        Assert.True(CalendarShareRules.ValidNarrowing(share, CalendarShareLevel.Level2, Today, Today.AddDays(30), Today));
        Assert.False(CalendarShareRules.ValidNarrowing(share, CalendarShareLevel.Level2, Today.AddDays(-1), Today.AddDays(30), Today));
        Assert.False(CalendarShareRules.ValidNarrowing(share, CalendarShareLevel.Level2, Today, Today.AddDays(31), Today));
        Assert.False(CalendarShareRules.ValidNarrowing(share, CalendarShareLevel.Level2, Today.AddDays(5), Today.AddDays(4), Today));

        var level1 = Share(CalendarShareStatus.Pending, CalendarShareLevel.Level1);
        Assert.False(CalendarShareRules.ValidNarrowing(level1, CalendarShareLevel.Level2, Today, Today.AddDays(30), Today));
    }

    [Fact]
    public void A_viewer_change_raises_the_level_or_extends_the_end_and_keeps_the_start()
    {
        var share = Share(level: CalendarShareLevel.Level1, startOffset: -100, endOffset: 30);
        Assert.True(CalendarShareRules.ValidViewerChange(share, CalendarShareLevel.Level2, share.EndDate, Today));
        Assert.True(CalendarShareRules.ValidViewerChange(share, CalendarShareLevel.Level1, Today.AddDays(60), Today));
        // Nothing grows.
        Assert.False(CalendarShareRules.ValidViewerChange(share, CalendarShareLevel.Level1, share.EndDate, Today));
        // Earlier end.
        Assert.False(CalendarShareRules.ValidViewerChange(share, CalendarShareLevel.Level2, Today.AddDays(10), Today));
        // Counted from the original start: -100 … 265 is 366 days.
        Assert.True(CalendarShareRules.ValidViewerChange(share, CalendarShareLevel.Level1, Today.AddDays(264), Today));
        Assert.False(CalendarShareRules.ValidViewerChange(share, CalendarShareLevel.Level1, Today.AddDays(265), Today));

        var level2 = Share(level: CalendarShareLevel.Level2);
        Assert.False(CalendarShareRules.ValidViewerChange(level2, CalendarShareLevel.Level1, Today.AddDays(60), Today));
    }

    [Fact]
    public void An_owner_change_lowers_the_level_or_moves_the_dates()
    {
        var share = Share(level: CalendarShareLevel.Level2, startOffset: -10, endOffset: 30);
        Assert.True(CalendarShareRules.ValidOwnerChange(share, CalendarShareLevel.Level1, share.StartDate, share.EndDate, Today));
        // The original start may stay in the past; a new start must be today or later.
        Assert.True(CalendarShareRules.ValidOwnerChange(share, CalendarShareLevel.Level2, share.StartDate, Today.AddDays(5), Today));
        Assert.True(CalendarShareRules.ValidOwnerChange(share, CalendarShareLevel.Level2, Today.AddDays(1), Today.AddDays(90), Today));
        Assert.False(CalendarShareRules.ValidOwnerChange(share, CalendarShareLevel.Level2, Today.AddDays(-5), share.EndDate, Today));
        // The end must not be past, nothing unchanged, never a higher level.
        Assert.False(CalendarShareRules.ValidOwnerChange(share, CalendarShareLevel.Level2, share.StartDate, Today.AddDays(-1), Today));
        Assert.False(CalendarShareRules.ValidOwnerChange(share, CalendarShareLevel.Level2, share.StartDate, share.EndDate, Today));
        var level1 = Share(level: CalendarShareLevel.Level1);
        Assert.False(CalendarShareRules.ValidOwnerChange(level1, CalendarShareLevel.Level2, level1.StartDate, level1.EndDate, Today));
    }

    [Fact]
    public void A_pending_change_is_checked_again_when_approved()
    {
        var share = Share(startOffset: -10, endOffset: 5, pendingChange: true);
        Assert.True(CalendarShareRules.ValidPendingApproval(share, Today));
        share.PendingEndDate = Today.AddDays(355); // -10 … 355 is 366 days
        Assert.False(CalendarShareRules.ValidPendingApproval(share, Today));
        share.PendingEndDate = Today.AddDays(-1);
        Assert.False(CalendarShareRules.ValidPendingApproval(share, Today));
    }

    [Fact]
    public void A_share_past_its_end_date_has_ended_with_nothing_left_to_do()
    {
        // BR-425 (PO 09/11/2026, Q-B): no Expired state — the end date alone decides, and only after the last day.
        var share = Share(endOffset: -1);
        Assert.True(CalendarShareRules.IsLapsed(share, Today));
        Assert.Equal(CalendarShareStatus.Effective, share.Status);
        Assert.Empty(CalendarShareRules.Actions(share, OwnerId, Today, false));
        Assert.Empty(CalendarShareRules.Actions(Share(CalendarShareStatus.Pending, endOffset: -1), OwnerId, Today, false));
        Assert.False(CalendarShareRules.IsLapsed(Share(endOffset: 0), Today));
    }

    [Fact]
    public void The_actions_follow_who_sent_it_and_what_is_pending()
    {
        string[] Of(CalendarShare share, Guid actor, bool senderDeactivated = false)
            => [.. CalendarShareRules.Actions(share, actor, Today, senderDeactivated)];

        var request = Share(CalendarShareStatus.Pending);
        Assert.Equal(["accept", "decline"], Of(request, OwnerId));
        Assert.Equal(["withdraw"], Of(request, ViewerId));
        // UC-84c EX-5: the sender was deactivated, so the invitation can only be declined.
        Assert.Equal(["decline"], Of(request, OwnerId, senderDeactivated: true));

        var direct = Share(CalendarShareStatus.Pending, initiatedBy: CalendarShareInitiator.Owner);
        Assert.Equal(["accept", "decline"], Of(direct, ViewerId));
        Assert.Equal(["withdraw"], Of(direct, OwnerId));

        var effective = Share();
        Assert.Equal(["update", "end"], Of(effective, OwnerId));
        Assert.Equal(["requestChange", "end"], Of(effective, ViewerId));

        var changing = Share(pendingChange: true);
        Assert.Equal(["accept", "decline", "update", "end"], Of(changing, OwnerId));
        Assert.Equal(["withdraw", "end"], Of(changing, ViewerId));

        Assert.Empty(Of(effective, Guid.CreateVersion7()));
        Assert.Empty(Of(Share(CalendarShareStatus.Stopped), OwnerId));
    }
    [Fact]
    public void Today_is_vietnams_date_not_the_servers()
    {
        // 23:30 UTC on the 7th is already 06:30 on the 8th in Hanoi.
        Assert.Equal(Today, VietnamTime.Today(new DateTimeOffset(2026, 11, 7, 23, 30, 0, TimeSpan.Zero)));
        Assert.Equal(Today.AddDays(-1), VietnamTime.Today(new DateTimeOffset(2026, 11, 7, 16, 59, 0, TimeSpan.Zero)));
        Assert.Equal(new DateOnly(2026, 11, 2), VietnamTime.Monday(Today)); // Sunday belongs to the week begun on Monday
        Assert.Equal(new DateOnly(2026, 11, 9), VietnamTime.Monday(new DateOnly(2026, 11, 13)));
        Assert.Equal(TimeSpan.FromHours(7), VietnamTime.At(Today, new TimeOnly(8, 0)).Offset);
    }
}
