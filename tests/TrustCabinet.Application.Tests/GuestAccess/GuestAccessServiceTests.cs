using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.GuestAccess;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Application.Tests.GuestAccess;

public sealed class GuestAccessServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeGuestSessionRepository _sessions = new() { Now = Now };
    private readonly FakeGuestCredentials _credentials = new();
    private readonly FakeGuestCodeSender _sender = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeAuditLog _audit = new();

    private static readonly GuestAccessOptions Limits = new();

    private GuestAccessService Service() => new(_sessions, _credentials, Options.Create(Limits), _sender,
        _unitOfWork, _audit, new FixedClock(Now), NullLogger<GuestAccessService>.Instance);

    private MeetingGuestInvitation GivenInvitation(MeetingStatus status = MeetingStatus.Published)
    {
        var meeting = new Meeting
        {
            Title = "Họp có Khách",
            Status = status,
            CreatedById = Guid.CreateVersion7(),
            PlannedStartTime = Now.AddHours(-1),
            PlannedEndTime = Now.AddHours(1),
        };
        var invitation = new MeetingGuestInvitation
        {
            MeetingId = meeting.Id, Meeting = meeting, InvitedName = "Khách mời",
            Email = "khach@guests.test", InvitedById = Guid.CreateVersion7(),
        };
        _sessions.Invitations.Add(invitation);
        _credentials.ValidLinks.Add(invitation.Id);
        return invitation;
    }

    private MeetingGuestSession GivenSession(MeetingGuestInvitation invitation, bool named = true)
    {
        var session = new GuestSession
        {
            InvitationId = invitation.Id, Invitation = invitation, CreatedAt = Now, UpdatedAt = Now,
            ExpiresAt = Now.AddHours(Limits.SessionLifetimeHours),
        };
        if (named) invitation.SelfDeclaredDisplayName = "Khách tự khai";
        invitation.SelfDeclaredJobTitle = "Chức vụ";
        _sessions.Sessions.Add(session);
        return new(invitation.MeetingId, invitation.Id, session.Id);
    }

    private GuestVerificationChallenge GivenChallenge(MeetingGuestInvitation invitation, string code,
        DateTimeOffset? createdAt = null, int failed = 0, bool consumed = false, bool expired = false)
    {
        var at = createdAt ?? Now;
        var challenge = new GuestVerificationChallenge
        {
            InvitationId = invitation.Id, CreatedAt = at, UpdatedAt = at,
            ExpiresAt = expired ? Now.AddMinutes(-1) : at.AddMinutes(Limits.CodeLifetimeMinutes),
            FailedAttempts = failed, ConsumedAt = consumed ? at : null,
            CodeHash = "",
        };
        // The hash is keyed by the challenge id, like the service mints it.
        challenge.CodeHash = _credentials.CodeHash(challenge.Id, code);
        _sessions.Challenges.Add(challenge);
        return challenge;
    }

    [Fact]
    public async Task Send_saves_a_challenge_and_mails_its_code_in_one_committed_transaction()
    {
        var invitation = GivenInvitation();

        var result = await Service().SendCodeAsync(invitation.Id, "link", default);

        var sent = Assert.IsType<GuestCodeSent>(result.Data);
        Assert.Equal((Limits.CodeLifetimeMinutes * 60, Limits.ResendIntervalSeconds),
            (sent.ExpiresInSeconds, sent.ResendAfterSeconds));
        var challenge = Assert.Single(_sessions.Challenges);
        Assert.Equal(invitation.Id, challenge.InvitationId);
        // The mailed code is the one the stored hash was minted from.
        Assert.Equal(6, _sender.Calls[0].Code.Length);
        Assert.Equal((challenge.Id, _sender.Calls[0].Code), (_credentials.Hashes[^1].ChallengeId, _credentials.Hashes[^1].Code));
        Assert.Equal(invitation.Email, _sender.Calls[0].ToAddress);
        Assert.Empty(_sessions.DeletedChallenges);
        Assert.Equal([TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal((1, 1, 0), (_unitOfWork.Saves, _unitOfWork.Commits, _unitOfWork.Rollbacks));
        Assert.Equal([(invitation.Id, true)], _sessions.Locks);
    }

    [Fact]
    public async Task Send_with_a_wrong_link_token_answers_invalid_without_touching_the_store()
    {
        var invitation = GivenInvitation();

        var result = await Service().SendCodeAsync(invitation.Id, "wrong", default);

        Assert.Equal(GuestAccessFailure.InvalidInvitation, result.Failure);
        Assert.Empty(_unitOfWork.Modes);
        Assert.Empty(_sender.Calls);
    }

    [Fact]
    public async Task Send_to_a_revoked_invitation_answers_invalid()
    {
        var invitation = GivenInvitation();
        invitation.RevokedAt = Now;

        var result = await Service().SendCodeAsync(invitation.Id, "link", default);

        Assert.Equal(GuestAccessFailure.InvalidInvitation, result.Failure);
        Assert.Empty(_sessions.Challenges);
    }

    [Fact]
    public async Task Send_right_after_a_code_answers_too_soon_with_a_retry_delay()
    {
        var invitation = GivenInvitation();
        GivenChallenge(invitation, "111111", createdAt: Now.AddSeconds(-10));

        var result = await Service().SendCodeAsync(invitation.Id, "link", default);

        Assert.Equal(GuestAccessFailure.TooSoon, result.Failure);
        Assert.True(result.RetryAfterSeconds > 0);
        Assert.Single(_sessions.Challenges);
    }

    [Fact]
    public async Task Send_past_the_daily_cap_answers_daily_limit()
    {
        var invitation = GivenInvitation();
        for (var i = 0; i < Limits.MaxCodesPerDay; i++)
            GivenChallenge(invitation, "111111", createdAt: Now.AddHours(-23).AddMinutes(-i));

        var result = await Service().SendCodeAsync(invitation.Id, "link", default);

        Assert.Equal(GuestAccessFailure.DailyLimit, result.Failure);
        Assert.True(result.RetryAfterSeconds > 0);
    }

    [Fact]
    public async Task Verify_with_the_live_code_opens_a_session_and_audits_it()
    {
        var invitation = GivenInvitation();
        GivenChallenge(invitation, "123456");

        var result = await Service().VerifyAsync(invitation.Id, "link", "123456", default);

        var verified = Assert.IsType<GuestVerified>(result.Data);
        Assert.Equal(invitation.MeetingId, verified.MeetingId);
        Assert.False(verified.ProfileCompleted);
        var session = Assert.Single(_sessions.Sessions);
        Assert.Equal($"token:{session.Id:D}:{session.ExpiresAt:O}", verified.AccessToken);
        Assert.True(session.ExpiresAt > Now);
        var entry = Assert.Single(_audit.Rows);
        Assert.Equal((AuditActions.GuestVerified, AuditActions.GuestSessionObjectType, session.Id),
            (entry.Action, entry.ObjectType, entry.ObjectId));
        Assert.Equal([TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal((1, 1, 0), (_unitOfWork.Saves, _unitOfWork.Commits, _unitOfWork.Rollbacks));
    }

    [Fact]
    public async Task Verify_with_a_wrong_code_counts_it_and_reports_what_is_left()
    {
        var invitation = GivenInvitation();
        var challenge = GivenChallenge(invitation, "123456");

        var result = await Service().VerifyAsync(invitation.Id, "link", "000000", default);

        Assert.Equal(GuestAccessFailure.InvalidCode, result.Failure);
        Assert.Equal(Limits.MaxFailedAttempts - 1, result.RemainingAttempts);
        Assert.Equal(1, challenge.FailedAttempts);
        Assert.Empty(_sessions.Sessions);
        Assert.Equal((1, 1, 0), (_unitOfWork.Saves, _unitOfWork.Commits, _unitOfWork.Rollbacks));
    }

    [Fact]
    public async Task Verify_with_the_replaced_code_is_refused_without_counting_it()
    {
        var invitation = GivenInvitation();
        GivenChallenge(invitation, "111111", createdAt: Now.AddMinutes(-5));
        var live = GivenChallenge(invitation, "123456", createdAt: Now);

        var result = await Service().VerifyAsync(invitation.Id, "link", "111111", default);

        Assert.Equal(GuestAccessFailure.InvalidCode, result.Failure);
        Assert.Equal(Limits.MaxFailedAttempts, result.RemainingAttempts);
        Assert.Equal(0, live.FailedAttempts);
    }

    [Fact]
    public async Task Verify_past_the_allowed_guesses_locks_the_code()
    {
        var invitation = GivenInvitation();
        GivenChallenge(invitation, "123456", failed: Limits.MaxFailedAttempts - 1);

        var result = await Service().VerifyAsync(invitation.Id, "link", "000000", default);

        Assert.Equal(GuestAccessFailure.CodeLocked, result.Failure);
        Assert.Null(result.RemainingAttempts);
    }

    [Fact]
    public async Task Verify_with_an_expired_code_answers_invalid_code()
    {
        var invitation = GivenInvitation();
        GivenChallenge(invitation, "123456", createdAt: Now.AddHours(-1), expired: true);

        var result = await Service().VerifyAsync(invitation.Id, "link", "123456", default);

        Assert.Equal(GuestAccessFailure.InvalidCode, result.Failure);
        Assert.Empty(_sessions.Sessions);
    }

    [Fact]
    public async Task Verify_with_a_malformed_code_is_refused_without_counting_it()
    {
        var invitation = GivenInvitation();
        var challenge = GivenChallenge(invitation, "123456");

        var result = await Service().VerifyAsync(invitation.Id, "link", "not-six", default);

        Assert.Equal(GuestAccessFailure.InvalidCode, result.Failure);
        Assert.Equal(Limits.MaxFailedAttempts, result.RemainingAttempts);
        Assert.Equal(0, challenge.FailedAttempts);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact]
    public async Task Profile_save_after_the_session_ended_answers_ended()
    {
        var invitation = GivenInvitation();
        var guest = GivenSession(invitation);
        _sessions.Sessions[0].RevokedAt = Now;

        var result = await Service().SaveProfileAsync(guest, "Khách tự khai", null, default);

        Assert.Equal(GuestAccessFailure.SessionEnded, result.Failure);
    }

    [Fact]
    public async Task Profile_save_with_a_blank_name_answers_invalid_profile_with_errors()
    {
        var invitation = GivenInvitation();
        var guest = GivenSession(invitation, named: false);
        invitation.SelfDeclaredDisplayName = null;

        var result = await Service().SaveProfileAsync(guest, "  ", null, default);

        Assert.Equal(GuestAccessFailure.InvalidProfile, result.Failure);
        Assert.True(result.Errors!.ContainsKey("displayName"));
    }

    [Fact]
    public async Task Profile_save_over_a_declared_name_is_locked_unless_it_repeats_it()
    {
        var invitation = GivenInvitation();
        var guest = GivenSession(invitation);
        invitation.SelfDeclaredJobTitle = "Chức vụ";

        var locked = await Service().SaveProfileAsync(guest, "Tên khác", null, default);
        var repeated = await Service().SaveProfileAsync(guest, "Khách tự khai", "Chức vụ", default);

        Assert.Equal(GuestAccessFailure.ProfileLocked, locked.Failure);
        Assert.Equal(GuestAccessFailure.None, repeated.Failure);
    }

    [Fact]
    public async Task Sign_out_revokes_the_session_without_a_transaction()
    {
        var invitation = GivenInvitation();
        var guest = GivenSession(invitation);

        var result = await Service().SignOutAsync(guest, default);

        Assert.Equal(GuestAccessFailure.None, result.Failure);
        Assert.NotNull(_sessions.Sessions[0].RevokedAt);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact]
    public async Task A_store_failure_on_send_answers_unavailable_and_rolls_back()
    {
        var invitation = GivenInvitation();
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var result = await Service().SendCodeAsync(invitation.Id, "link", default);

        Assert.Equal(GuestAccessFailure.Unavailable, result.Failure);
        Assert.Equal((0, 0, 1), (_unitOfWork.Saves, _unitOfWork.Commits, _unitOfWork.Rollbacks));
        Assert.Empty(_sender.Calls);
    }

    [Fact]
    public async Task An_unsent_code_is_deleted_so_the_guest_may_retry_at_once()
    {
        var invitation = GivenInvitation();
        _sender.Sent = false;

        var result = await Service().SendCodeAsync(invitation.Id, "link", default);

        Assert.Equal(GuestAccessFailure.Unavailable, result.Failure);
        Assert.Empty(_sessions.Challenges);
        Assert.NotEmpty(_sessions.DeletedChallenges);
    }
}
