using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TrustCabinet.Domain.Authentication;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Users;
using TrustCabinet.Infrastructure.Authentication;

namespace TrustCabinet.Web.Endpoints;

/// <summary>
/// UC-01's <c>:Auth Controller</c> — translates HTTP into a call on the authentication service,
/// and the service's outcome into a status code and a message code.
///
/// It holds no authentication logic of its own: which branch applies is the
/// service's decision, and this file only decides how each one looks on the wire.
/// </summary>
public static class AuthEndpoints
{
    /// <summary>
    /// Message codes from the UC-01 specification. The response carries the code and a Vietnamese
    /// message: the code is what the frontend and the test suite match on, so rewording the text
    /// never breaks either.
    ///
    /// UC-01's MSG04 ("no system access") is gone with SCRUM-63 — see <c>SignInOutcome</c>.
    /// </summary>
    private const string Msg01InvalidCredentials = "MSG01";
    private const string Msg03AccountUnavailable = "MSG03";

    /// <summary>
    /// UC-02 numbers its messages differently from UC-01 for the same situations, so its codes are
    /// separate constants rather than reused ones: a deactivated account is MSG03 in UC-01 and
    /// MSG04 here. Do not "tidy" these into one set — the two use-case specifications are what the
    /// tester checks against. MSG05 went with UC-01's MSG04.
    /// </summary>
    private const string Google01SignInUnsuccessful = "MSG01";
    private const string Google03NotAuthorized = "MSG03";
    private const string Google04AccountUnavailable = "MSG04";

    /// <summary>
    /// The refresh token's cookie. <c>HttpOnly</c> is the point of it: a token JavaScript cannot
    /// read is a token an XSS on the page cannot steal. <c>Path=/api/auth</c> keeps it off every
    /// other request, and <c>SameSite=Strict</c> keeps another site from spending it.
    ///
    /// No <c>__Host-</c> prefix: that prefix demands <c>Path=/</c>, which would send the token with
    /// every API call.
    /// </summary>
    internal const string RefreshTokenCookie = "tc_refresh_token";
    private const string RefreshTokenCookiePath = "/api/auth";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Authentication");

        auth.MapPost("/login", SignInAsync).AllowAnonymous();
        auth.MapPost("/google", SignInWithGoogleAsync).AllowAnonymous();
        auth.MapPost("/refresh", RefreshAsync).AllowAnonymous();
        auth.MapPost("/logout", SignOutAsync).AllowAnonymous();
        auth.MapGet("/me", Me).RequireAuthorization();

        return app;
    }

    private static async Task<IResult> SignInAsync(
        [FromBody] SignInRequest request,
        IAuthenticationService authentication,
        ITokenIssuer tokenIssuer,
        HttpResponse response,
        CancellationToken ct)
    {
        // Blank input is the same answer as wrong input: an empty password must not be
        // distinguishable from an incorrect one.
        var result = await authentication.SignInAsync(request.Email ?? "", request.Password ?? "", ct);

        return result.Succeeded
            ? Issued(SignInResponse.From(result), result.RefreshToken!, tokenIssuer, response)
            : Rejected(result.Outcome);
    }

    /// <summary>
    /// UC-02's inner alt block. The outer one — Google sign-in failing or being cancelled — never
    /// reaches here: the browser gets no ID token to send, so the login page shows that message
    /// on its own.
    /// </summary>
    private static async Task<IResult> SignInWithGoogleAsync(
        [FromBody] GoogleSignInRequest request,
        IAuthenticationService authentication,
        IOptions<GoogleAuthenticationOptions> googleOptions,
        ITokenIssuer tokenIssuer,
        HttpResponse response,
        CancellationToken ct)
    {
        // Not configured means the feature does not exist, so it answers like any unknown URL
        // rather than announcing a disabled endpoint. Same reasoning as RequireCapability
        // rendering a 404 instead of a 403.
        if (!googleOptions.Value.Enabled)
        {
            return Results.NotFound();
        }

        var result = await authentication.SignInWithGoogleAsync(request.IdToken ?? "", ct);

        if (result.Succeeded)
        {
            return Issued(SignInResponse.FromGoogle(result), result.RefreshToken!, tokenIssuer, response);
        }

        var (code, message) = result.Outcome switch
        {
            GoogleSignInOutcome.NotLinked =>
                (Google03NotAuthorized, "Tài khoản Google này chưa được cấp quyền truy cập hệ thống."),
            GoogleSignInOutcome.AccountDeactivated =>
                (Google04AccountUnavailable, "Tài khoản không khả dụng. Vui lòng liên hệ quản trị viên."),
            // InvalidGoogleToken, and defensively any outcome added later without a case here.
            _ => (Google01SignInUnsuccessful, "Đăng nhập bằng Google không thành công."),
        };

        return Results.Json(
            new AuthErrorResponse(code, message), statusCode: StatusCodes.Status401Unauthorized);
    }

    /// <summary>
    /// The refresh token comes from the cookie only. No cookie is answered exactly like an expired
    /// token — MSG01 — since from here the two are indistinguishable anyway.
    /// </summary>
    private static async Task<IResult> RefreshAsync(
        IAuthenticationService authentication,
        ITokenIssuer tokenIssuer,
        HttpContext http,
        CancellationToken ct)
    {
        var result = await authentication.RefreshAsync(PresentedRefreshToken(http.Request), ct);

        return result.Succeeded
            ? Issued(SignInResponse.From(result), result.RefreshToken!, tokenIssuer, http.Response)
            : Rejected(result.Outcome);
    }

    private static async Task<IResult> SignOutAsync(
        IAuthenticationService authentication,
        HttpContext http,
        CancellationToken ct)
    {
        await authentication.SignOutAsync(PresentedRefreshToken(http.Request), ct);

        // Cleared unconditionally, valid token or not: whatever the browser holds is of no further
        // use once the user has signed out, and leaving it would have the next refresh try it.
        http.Response.Cookies.Append(
            RefreshTokenCookie, "", RefreshTokenCookieOptions(TimeSpan.Zero));

        // 204 whether or not the token was still valid: signing out is idempotent, and reporting
        // "that token was already gone" would only confirm token states to a caller.
        return Results.NoContent();
    }

    /// <summary>
    /// Who you are and what you may do, read from the database rather than from the token.
    ///
    /// It used to answer from the token's claims, which was cheaper and wrong. SCRUM-12 makes the
    /// database the authority precisely because an access token is a snapshot taken at sign-in:
    /// answering from it would tell an administrator who has just been deactivated — or whose role
    /// has just lost a capability via the SCRUM-60 screen — that they still hold it, for as long as
    /// the token lives. This endpoint is what the interface renders from, so that answer would
    /// paint a full admin console for someone every endpoint then refuses.
    ///
    /// The same read the capability gate does, for the same reason. One extra query per call, on an
    /// endpoint a browser calls once per page load.
    /// </summary>
    private static async Task<IResult> Me(
        ClaimsPrincipal principal, IUserAccountRepository users, CancellationToken ct)
    {
        var callerId = CapabilityGate.CurrentUserId(principal);

        // A validated token with no usable "sub". Refused rather than answered with blanks: the
        // capability gate rejects such a token everywhere else, and this endpoint has no business
        // being the one place it is accepted.
        if (callerId is null)
        {
            return Results.Unauthorized();
        }

        var caller = await users.FindByIdAsync(callerId.Value, ct);

        // 401 rather than 403: there is no longer an account behind this token, so the session
        // itself is over. The client's move is to sign in again, which is what 401 tells it.
        if (caller is null || !caller.IsActive)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(CurrentUserResponse.From(
            caller, await users.GetCapabilitiesAsync(caller.RoleId, ct)));
    }

    /// <summary>
    /// Deliberately no body fallback: a refresh token JavaScript can send is a refresh token
    /// JavaScript can hold, which is what the cookie exists to prevent.
    /// </summary>
    private static string PresentedRefreshToken(HttpRequest request)
        => request.Cookies[RefreshTokenCookie] ?? "";

    /// <summary>The access token and the user in the body; the refresh token only in the cookie.</summary>
    private static IResult Issued(
        SignInResponse body, string refreshToken, ITokenIssuer tokenIssuer, HttpResponse response)
    {
        response.Cookies.Append(
            RefreshTokenCookie,
            refreshToken,
            RefreshTokenCookieOptions(tokenIssuer.RefreshTokenLifetime));

        return Results.Ok(body);
    }

    /// <summary>
    /// <c>Secure</c> even in development: browsers treat http://localhost as a secure context and
    /// accept it there, so there is no reason for a weaker cookie anywhere.
    /// </summary>
    private static CookieOptions RefreshTokenCookieOptions(TimeSpan maxAge) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = RefreshTokenCookiePath,
        MaxAge = maxAge,
    };

    /// <summary>
    /// The failing branches of UC-01. All 401: the distinction between them is in the
    /// message code, and none of them reveals whether the email exists.
    /// </summary>
    private static IResult Rejected(SignInOutcome outcome)
    {
        var (code, message) = outcome switch
        {
            SignInOutcome.AccountDeactivated =>
                (Msg03AccountUnavailable, "Tài khoản không khả dụng. Vui lòng liên hệ quản trị viên."),
            // Covers InvalidCredentials and, defensively, an outcome added later without a case
            // here: the safe default for an unknown authentication outcome is to refuse.
            _ => (Msg01InvalidCredentials, "Email hoặc mật khẩu không đúng."),
        };

        return Results.Json(new AuthErrorResponse(code, message), statusCode: StatusCodes.Status401Unauthorized);
    }
}

/// <summary>Sign-in payload. Both fields are nullable so a malformed body is a 401, not a 400.</summary>
public sealed record SignInRequest(string? Email, string? Password);

/// <summary>
/// The Google ID token the browser obtained from Google Identity Services. Nullable so a malformed
/// body answers MSG01 like any other unusable token, rather than a 400 that distinguishes itself.
/// </summary>
public sealed record GoogleSignInRequest(string? IdToken);

public sealed record AuthErrorResponse(string Code, string Message);

/// <summary>
/// What a successful sign-in returns. No refresh token: that travels only in the HttpOnly cookie,
/// out of reach of any script on the page.
/// </summary>
public sealed record SignInResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    CurrentUserResponse User)
{
    public static SignInResponse From(Domain.Authentication.SignInResult result) => new(
        result.AccessToken!,
        result.AccessTokenExpiresAt!.Value,
        CurrentUserResponse.From(result.User!, result.Capabilities ?? []));

    /// <summary>
    /// Identical shape for a Google sign-in: the client should not need to care which way the
    /// session was established, only that it now has one.
    /// </summary>
    public static SignInResponse FromGoogle(GoogleSignInResult result) => new(
        result.AccessToken!,
        result.AccessTokenExpiresAt!.Value,
        CurrentUserResponse.From(result.User!, result.Capabilities ?? []));
}

/// <summary>
/// Who is signed in. The role travels as its id and its current name (ADR-0008): the id is what
/// other endpoints speak, the name is what the screen shows.
/// </summary>
public sealed record CurrentUserResponse(
    string Id,
    string Email,
    string FullName,
    string RoleId,
    string RoleName,
    string[] Capabilities)
{
    /// <summary>Needs <see cref="User.Role"/> loaded — every repository behind these endpoints includes it.</summary>
    public static CurrentUserResponse From(User user, IReadOnlyCollection<Capability> capabilities) => new(
        user.Id.ToString(),
        user.Email,
        user.FullName,
        user.RoleId.ToString(),
        user.Role?.Name
            ?? throw new InvalidOperationException(
                $"User {user.Id} was loaded without its role; include User.Role in the query."),
        [.. capabilities.Select(c => c.ToString())]);
}
