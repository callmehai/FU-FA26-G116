namespace TrustCabinet.Application.GuestAccess;

/// <summary>The guest token's claims, read back by the web layer's identity resolver.</summary>
public static class GuestTokenClaims
{
    public const string SubjectPrefix = "guest:";
    public const string Meeting = "guest_meeting";
    public const string Invitation = "guest_invitation";
    public const string Session = "guest_session";
}
