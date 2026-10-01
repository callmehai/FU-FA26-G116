using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Web.Tests.Meetings;

public sealed class MeetingInvitationRecipientPolicyTests
{
    [Fact]
    public void Delegate_is_not_added_by_reference_but_qualifies_with_own_active_non_delegated_row()
    {
        var meetingId = Guid.CreateVersion7();
        var delegateUser = NewUser("delegate@trustcabinet.local");
        var sourceParticipant = NewParticipant(
            meetingId, NewUser("source@trustcabinet.local"), InvitationResponse.Delegated);
        sourceParticipant.DelegatedToUserId = delegateUser.Id;
        var participants = new List<MeetingParticipant> { sourceParticipant };

        Assert.Empty(MeetingInvitationRecipientPolicy.SelectSnapshot(participants));

        var delegateParticipant = NewParticipant(
            meetingId, delegateUser, InvitationResponse.Pending);
        participants.Add(delegateParticipant);

        Assert.Equal(
            delegateUser.Id,
            Assert.Single(MeetingInvitationRecipientPolicy.SelectSnapshot(participants)).UserId);
    }

    [Fact]
    public void Snapshot_excludes_inactive_users_and_duplicate_rows_by_user_id()
    {
        var meetingId = Guid.CreateVersion7();
        var inactive = NewUser("inactive@trustcabinet.local", isActive: false);
        var active = NewUser("active@trustcabinet.local");
        var participants = new[]
        {
            NewParticipant(meetingId, inactive, InvitationResponse.Pending),
            NewParticipant(meetingId, active, InvitationResponse.Pending),
            NewParticipant(meetingId, active, InvitationResponse.Accepted),
        };

        Assert.Equal(
            active.Id,
            Assert.Single(MeetingInvitationRecipientPolicy.SelectSnapshot(participants)).UserId);
    }

    private static User NewUser(string email, bool isActive = true) => new()
    {
        FullName = email,
        Email = email,
        PasswordHash = "test-hash",
        IsActive = isActive,
    };

    private static MeetingParticipant NewParticipant(
        Guid meetingId,
        User user,
        InvitationResponse response) => new()
        {
            MeetingId = meetingId,
            UserId = user.Id,
            User = user,
            Response = response,
        };
}
