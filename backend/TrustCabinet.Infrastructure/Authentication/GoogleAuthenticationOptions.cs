using System.ComponentModel.DataAnnotations;

namespace TrustCabinet.Infrastructure.Authentication;

/// <summary>
/// Google sign-in settings, bound from the <c>Authentication:Google</c> configuration section.
/// </summary>
public sealed class GoogleAuthenticationOptions
{
    public const string SectionName = "Authentication:Google";

    /// <summary>
    /// The OAuth client id from Google Cloud Console, and the audience every accepted ID token
    /// must name.
    ///
    /// Not a secret — the browser sends it to Google in plain sight, so it may live in
    /// appsettings.json. What must never be committed is the client *secret*, which this flow does
    /// not use at all: the browser obtains the ID token and the backend only verifies it.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Whether Google sign-in is offered. Off by default so the application still starts on a
    /// machine with no Google credentials configured — /api/auth/google then answers 404, and
    /// password sign-in is unaffected.
    /// </summary>
    public bool Enabled { get; set; }
}
