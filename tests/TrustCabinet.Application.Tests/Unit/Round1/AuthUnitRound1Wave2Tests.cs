using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Authentication;
using TrustCabinet.Application.GuestAccess;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Application.Tests.Unit.Round1;

// Round 1 causation tests for FE-01 (Authentication), wave 2: the UTC left over after wave 1.
// Each test carries BOTH [Trait("UTC", "<Class.Method>/<UTCnnn>")] (what apply-trx matches on,
// since the trx carries no trait) AND [Trait("Trait", "UTC")] (so --filter "Trait=UTC" selects
// them), plus a DisplayName containing the same "<Class.Method>/<UTCnnn>" string.
// Tester: Phạm Gia Khánh.
public sealed class AuthUnitRound1Wave2Tests
{
    private readonly FakeAccounts _users = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeRefreshTokenStore _refreshTokens = new();
    private readonly FakeGoogleTokenVerifier _google = new();
    private readonly FakeAuditService _audit = new();

    private AuthenticationService AuthService() => new(
        _users, _unitOfWork, new FakePasswordHasher(), new FakeTokenIssuer(), _refreshTokens, _google, _audit,
        NullLogger<AuthenticationService>.Instance);

    [Fact(DisplayName = "AuthenticationService.RefreshAsync/UTC002 whitespace token is refused without touching the store")]
    [Trait("UTC", "AuthenticationService.RefreshAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task RefreshAsync_UTC002()
    {
        var user = _users.Add("organizer@example.com", BuiltInRoles.Member);
        var signIn = await AuthService().SignInAsync(user.Email, "secret");

        var result = await AuthService().RefreshAsync("   ");

        Assert.Equal(SignInOutcome.InvalidCredentials, result.Outcome);
        Assert.Equal(user.Id, _refreshTokens.Live[signIn.RefreshToken!]);
        Assert.Single(_refreshTokens.Live);
    }

    [Fact(DisplayName = "AuthenticationService.SignInAsync/UTC004 blank email is refused without reading the store")]
    [Trait("UTC", "AuthenticationService.SignInAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task SignInAsync_UTC004()
    {
        // Would throw on any read: returning normally proves the store was not read.
        _users.Fail = new PersistenceUnavailableException();

        var result = await AuthService().SignInAsync("", "anything");

        Assert.Equal(SignInOutcome.InvalidCredentials, result.Outcome);
        Assert.Empty(_refreshTokens.Live);
        Assert.Empty(_audit.Actions);
    }

    [Fact(DisplayName = "AuthenticationService.SignInAsync/UTC005 active account with a wrong password is refused and recorded")]
    [Trait("UTC", "AuthenticationService.SignInAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task SignInAsync_UTC005()
    {
        _users.Add("organizer@example.com", BuiltInRoles.Member);

        var result = await AuthService().SignInAsync("organizer@example.com", "wrong-password");

        Assert.Equal(SignInOutcome.InvalidCredentials, result.Outcome);
        Assert.Null(result.RefreshToken);
        Assert.Empty(_refreshTokens.Live);
        // BR-009: an attempt on an existing account is recorded (reason invalid_password).
        Assert.Equal([AuditActions.SignInFailed], _audit.Actions);
    }

    [Fact(DisplayName = "AuthenticationService.SignInAsync/UTC006 deactivated account with a wrong password reveals nothing")]
    [Trait("UTC", "AuthenticationService.SignInAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task SignInAsync_UTC006()
    {
        _users.Add("former@example.com", BuiltInRoles.Member, isActive: false);

        var result = await AuthService().SignInAsync("former@example.com", "wrong-password");

        // Credentials are checked before the account state: MSG01, never MSG02, so the caller cannot tell
        // the account exists but is deactivated.
        Assert.Equal(SignInOutcome.InvalidCredentials, result.Outcome);
        Assert.Null(result.RefreshToken);
        Assert.Equal([AuditActions.SignInFailed], _audit.Actions);
    }

    [Fact(DisplayName = "AuthenticationService.SignInWithGoogleAsync/UTC002 deactivated never-linked account is refused without linking")]
    [Trait("UTC", "AuthenticationService.SignInWithGoogleAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task SignInWithGoogleAsync_UTC002()
    {
        var user = _users.Add("former@example.com", BuiltInRoles.Member, isActive: false);
        _google.Identity = new GoogleIdentity("g-300", "former@example.com");

        var result = await AuthService().SignInWithGoogleAsync("id-token");

        Assert.Equal(GoogleSignInOutcome.AccountDeactivated, result.Outcome);
        Assert.Null(user.GoogleSubjectId);
        Assert.Empty(_refreshTokens.Live);
    }

    [Fact(DisplayName = "AuthenticationService.SignInWithGoogleAsync/UTC004 linked account with no capabilities signs in empty")]
    [Trait("UTC", "AuthenticationService.SignInWithGoogleAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task SignInWithGoogleAsync_UTC004()
    {
        var user = _users.Add("chutoa@gmail.com", BuiltInRoles.Member, passwordHash: null);
        user.GoogleSubjectId = "g-100";
        _google.Identity = new GoogleIdentity("g-100", "chutoa@gmail.com");

        var result = await AuthService().SignInWithGoogleAsync("id-token");

        Assert.Equal(GoogleSignInOutcome.Success, result.Outcome);
        Assert.Empty(result.Capabilities!);
        Assert.Equal(user.Id, _refreshTokens.Live[result.RefreshToken!]);
    }

    [Fact(DisplayName = "AuthenticationService.SignOutAsync/UTC002 unknown token completes and keeps live sessions")]
    [Trait("UTC", "AuthenticationService.SignOutAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task SignOutAsync_UTC002()
    {
        var user = _users.Add("organizer@example.com", BuiltInRoles.Member);
        var signIn = await AuthService().SignInAsync(user.Email, "secret");

        await AuthService().SignOutAsync("dead-token");

        Assert.Equal(user.Id, _refreshTokens.Live[signIn.RefreshToken!]);
        Assert.Single(_refreshTokens.Live);
        Assert.Empty(_refreshTokens.RevokedAll);
    }

    private static readonly DateTimeOffset GuestNow = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeGuestSessionRepository _sessions = new();
    private readonly FakeGuestCredentials _credentials = new();
    private readonly FakeGuestCodeSender _sender = new();
    private readonly FakeUnitOfWork _guestWork = new();
    private readonly FakeAuditLog _guestAudit = new();
    private static readonly GuestAccessOptions Limits = new();

    private GuestAccessService GuestService() => new(_sessions, _credentials, Options.Create(Limits), _sender,
        _guestWork, _guestAudit, new FixedClock(GuestNow), NullLogger<GuestAccessService>.Instance);

    private MeetingGuestInvitation GivenInvitation(string email = "nguoimoi@example.com")
    {
        _sessions.Now = GuestNow;
        var meeting = new Meeting
        {
            Title = "Họp giao ban",
            Status = MeetingStatus.Published,
            CreatedById = Guid.CreateVersion7(),
            PlannedStartTime = GuestNow.AddHours(-1),
            PlannedEndTime = GuestNow.AddHours(1),
        };
        var invitation = new MeetingGuestInvitation
        {
            MeetingId = meeting.Id, Meeting = meeting, InvitedName = "Khách mời",
            Email = email, InvitedById = Guid.CreateVersion7(),
        };
        _sessions.Invitations.Add(invitation);
        _credentials.ValidLinks.Add(invitation.Id);
        return invitation;
    }

    private GuestVerificationChallenge GivenChallenge(MeetingGuestInvitation invitation, string code, DateTimeOffset createdAt)
    {
        var challenge = new GuestVerificationChallenge
        {
            InvitationId = invitation.Id, CreatedAt = createdAt, UpdatedAt = createdAt,
            ExpiresAt = createdAt.AddMinutes(Limits.CodeLifetimeMinutes), CodeHash = "",
        };
        challenge.CodeHash = _credentials.CodeHash(challenge.Id, code);
        _sessions.Challenges.Add(challenge);
        return challenge;
    }

    [Fact(DisplayName = "GuestAccessService.OpenAsync/UTC003 unreadable invitation store is answered unavailable")]
    [Trait("UTC", "GuestAccessService.OpenAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task OpenAsync_UTC003()
    {
        var invitation = GivenInvitation();
        _sessions.Fail = new PersistenceUnavailableException();

        var result = await GuestService().OpenAsync(invitation.Id, "link", default);

        Assert.Equal(GuestAccessFailure.Unavailable, result.Failure);
        Assert.Null(result.Data);
        Assert.Empty(_guestAudit.Rows);
    }

    [Fact(DisplayName = "GuestAccessService.OpenAsync/UTC004 three-letter local part masks to one leading character")]
    [Trait("UTC", "GuestAccessService.OpenAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task OpenAsync_UTC004()
    {
        var invitation = GivenInvitation("abc@example.com");

        var result = await GuestService().OpenAsync(invitation.Id, "link", default);

        var view = Assert.IsType<GuestInvitationView>(result.Data);
        Assert.Equal("a***@example.com", view.MaskedEmail);
        Assert.Equal(GuestAccessFailure.None, result.Failure);
    }

    [Fact(DisplayName = "GuestAccessService.SendCodeAsync/UTC002 wrong link token creates and sends nothing")]
    [Trait("UTC", "GuestAccessService.SendCodeAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task SendCodeAsync_UTC002()
    {
        var invitation = GivenInvitation();

        var result = await GuestService().SendCodeAsync(invitation.Id, "wrong-token", default);

        Assert.Equal(GuestAccessFailure.InvalidInvitation, result.Failure);
        Assert.Empty(_sessions.Challenges);
        Assert.Empty(_sender.Calls);
    }

    [Fact(DisplayName = "GuestAccessService.SendCodeAsync/UTC004 code at exactly the resend boundary is sent again")]
    [Trait("UTC", "GuestAccessService.SendCodeAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task SendCodeAsync_UTC004()
    {
        var invitation = GivenInvitation();
        GivenChallenge(invitation, "000000", GuestNow.AddSeconds(-60));

        var result = await GuestService().SendCodeAsync(invitation.Id, "link", default);

        Assert.IsType<GuestCodeSent>(result.Data);
        Assert.Single(_sender.Calls);
        Assert.Equal(2, _sessions.Challenges.Count);
    }

    [Fact(DisplayName = "GuestAccessService.SendCodeAsync/UTC005 twentieth code in 24 hours hits the daily cap")]
    [Trait("UTC", "GuestAccessService.SendCodeAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task SendCodeAsync_UTC005()
    {
        var invitation = GivenInvitation();
        var newest = GuestNow.AddMinutes(-2);
        var oldest = GuestNow.AddHours(-23);
        for (var i = 0; i < Limits.MaxCodesPerDay; i++)
            GivenChallenge(invitation, "111111", oldest.AddSeconds((newest - oldest).TotalSeconds * i / 19));

        var result = await GuestService().SendCodeAsync(invitation.Id, "link", default);

        Assert.Equal(GuestAccessFailure.DailyLimit, result.Failure);
        Assert.Equal(3600, result.RetryAfterSeconds);
        Assert.Empty(_sender.Calls);
    }

    [Fact(DisplayName = "GuestAccessService.VerifyAsync/UTC002 wrong link token reads no challenge")]
    [Trait("UTC", "GuestAccessService.VerifyAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task VerifyAsync_UTC002()
    {
        var invitation = GivenInvitation();

        var result = await GuestService().VerifyAsync(invitation.Id, "wrong-token", "123456", default);

        Assert.Equal(GuestAccessFailure.InvalidInvitation, result.Failure);
        Assert.Null(result.Data);
        Assert.Empty(_sessions.Locks);
    }

    [Fact(DisplayName = "GuestAccessService.VerifyAsync/UTC004 code expiring exactly now is refused")]
    [Trait("UTC", "GuestAccessService.VerifyAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task VerifyAsync_UTC004()
    {
        var invitation = GivenInvitation();
        var challenge = new GuestVerificationChallenge
        {
            InvitationId = invitation.Id, CreatedAt = GuestNow.AddMinutes(-15), UpdatedAt = GuestNow.AddMinutes(-15),
            ExpiresAt = GuestNow, CodeHash = "",
        };
        challenge.CodeHash = _credentials.CodeHash(challenge.Id, "123456");
        _sessions.Challenges.Add(challenge);

        var result = await GuestService().VerifyAsync(invitation.Id, "link", "123456", default);

        Assert.Equal(GuestAccessFailure.InvalidCode, result.Failure);
        Assert.Null(result.RemainingAttempts);
        Assert.Empty(_sessions.Sessions);
    }

    [Fact(DisplayName = "GuestAccessService.VerifyAsync/UTC005 fifth failed guess locks the code")]
    [Trait("UTC", "GuestAccessService.VerifyAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task VerifyAsync_UTC005()
    {
        var invitation = GivenInvitation();
        var challenge = GivenChallenge(invitation, "123456", GuestNow);
        challenge.FailedAttempts = Limits.MaxFailedAttempts;

        var result = await GuestService().VerifyAsync(invitation.Id, "link", "123456", default);

        Assert.Equal(GuestAccessFailure.CodeLocked, result.Failure);
        Assert.Null(result.RemainingAttempts);
        Assert.Empty(_sessions.Sessions);
    }
}
