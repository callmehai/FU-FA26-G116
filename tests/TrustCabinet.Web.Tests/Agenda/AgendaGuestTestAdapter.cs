using System.Collections.Concurrent;
using System.Security.Claims;
using TrustCabinet.Domain.Agenda;

namespace TrustCabinet.Web.Tests.Agenda;

// An in-memory stand-in for UC-04, not a production invitation/session implementation.
internal sealed class AgendaGuestTestAdapter : IAgendaGuestAccess, IAgendaGuestIdentityResolver
{
    internal sealed record Access(AgendaGuestSession Identity, DateTimeOffset ExpiresAt,
        bool Verified = true, bool Revoked = false);
    internal readonly ConcurrentDictionary<Guid, Access> Sessions = new();
    internal TimeProvider Clock = TimeProvider.System;
    internal bool FailRead;
    internal bool FailReaders;

    public AgendaGuestSession? Resolve(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true || !principal.HasClaim("test_guest", "true")) return null;
        static Guid Id(ClaimsPrincipal p, string type) => Guid.TryParse(p.FindFirstValue(type), out var id) ? id : Guid.Empty;
        return new(Id(principal, "test_meeting"), Id(principal, "test_invitation"), Id(principal, "test_session"));
    }

    public Task<bool> CanReadAsync(AgendaGuestSession session, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (FailRead) throw new Npgsql.NpgsqlException("Injected guest authorization failure.");
        return Task.FromResult(Sessions.TryGetValue(session.SessionId, out var value)
            && value.Identity == session && Active(value));
    }

    public Task<IReadOnlyList<AgendaGuestSession>> ListReadersAsync(Guid meetingId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (FailReaders) throw new Npgsql.NpgsqlException("Injected guest recipient failure.");
        return Task.FromResult<IReadOnlyList<AgendaGuestSession>>(Sessions.Values
            .Where(v => v.Identity.MeetingId == meetingId && Active(v)).Select(v => v.Identity).ToArray());
    }

    private bool Active(Access access) => access.Verified && !access.Revoked && access.ExpiresAt > Clock.GetUtcNow();
}
