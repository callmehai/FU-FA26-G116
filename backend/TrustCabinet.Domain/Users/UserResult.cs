namespace TrustCabinet.Domain.Users;

/// <summary>
/// What <see cref="IUserService"/> hands back to the endpoint: which branch happened, plus the
/// account when the branch produced one.
///
/// Mirrors <c>SignInResult</c>: on failure <see cref="Account"/> is null, so an endpoint cannot
/// accidentally serialise an account for an operation that was refused.
/// </summary>
/// <param name="Errors">
/// Field name → why it is invalid, for <see cref="UserOutcome.InvalidInformation"/>. The diagram's
/// "Display invalid fields" needs to know *which* fields, so a single message would not do.
/// Empty for every other outcome.
/// </param>
public sealed record UserResult(
    UserOutcome Outcome,
    UserAccount? Account = null,
    IReadOnlyDictionary<string, string>? Errors = null)
{
    public bool Succeeded => Outcome == UserOutcome.Success;

    public static UserResult Success(UserAccount account) => new(UserOutcome.Success, account);

    public static UserResult Failure(UserOutcome outcome) => new(outcome);

    public static UserResult Invalid(IReadOnlyDictionary<string, string> errors)
        => new(UserOutcome.InvalidInformation, Errors: errors);
}
