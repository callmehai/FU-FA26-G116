using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Infrastructure.Audit;

namespace TrustCabinet.Infrastructure;

/// <summary>
/// The application's only database context. Entity shapes live in one configuration class each,
/// under Configurations/, rather than in a growing OnModelCreating.
///
/// <paramref name="caller"/> is optional so a test or the design-time factory can build one from options alone; the
/// web app's container supplies it, and audit rows then carry the request's IP address.
/// </summary>
public class TrustCabinetDbContext(DbContextOptions<TrustCabinetDbContext> options, ICallerContext? caller = null)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RoleCapability> RoleCapabilities => Set<RoleCapability>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<MeetingParticipant> MeetingParticipants => Set<MeetingParticipant>();
    public DbSet<MeetingGuestInvitation> MeetingGuestInvitations => Set<MeetingGuestInvitation>();
    public DbSet<MeetingInvitationDispatch> MeetingInvitationDispatches => Set<MeetingInvitationDispatch>();
    public DbSet<GuestVerificationChallenge> GuestVerificationChallenges => Set<GuestVerificationChallenge>();
    public DbSet<GuestSession> GuestSessions => Set<GuestSession>();
    public DbSet<MeetingStatusHistory> MeetingStatusHistory => Set<MeetingStatusHistory>();
    public DbSet<AgendaItem> AgendaItems => Set<AgendaItem>();
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
    public DbSet<Folder> Folders => Set<Folder>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<MeetingDocument> MeetingDocuments => Set<MeetingDocument>();
    public DbSet<MeetingDocumentAgendaItem> MeetingDocumentAgendaItems => Set<MeetingDocumentAgendaItem>();
    public DbSet<DocumentComment> DocumentComments => Set<DocumentComment>();
    public DbSet<SpeakingRequest> SpeakingRequests => Set<SpeakingRequest>();
    public DbSet<MeetingNote> MeetingNotes => Set<MeetingNote>();
    public DbSet<VotingSession> VotingSessions => Set<VotingSession>();
    public DbSet<VotingOption> VotingOptions => Set<VotingOption>();
    public DbSet<VoteEligibleVoter> VoteEligibleVoters => Set<VoteEligibleVoter>();
    public DbSet<VoteDocument> VoteDocuments => Set<VoteDocument>();
    public DbSet<DocumentReadConfirmation> DocumentReadConfirmations => Set<DocumentReadConfirmation>();
    public DbSet<VoteReceipt> VoteReceipts => Set<VoteReceipt>();
    public DbSet<VoteChoice> VoteChoices => Set<VoteChoice>();
    public DbSet<Recording> Recordings => Set<Recording>();
    public DbSet<Transcript> Transcripts => Set<Transcript>();
    public DbSet<TranscriptSegment> TranscriptSegments => Set<TranscriptSegment>();
    public DbSet<TranscriptSpeaker> TranscriptSpeakers => Set<TranscriptSpeaker>();
    public DbSet<SpeechClip> SpeechClips => Set<SpeechClip>();
    public DbSet<VoiceConsent> VoiceConsents => Set<VoiceConsent>();
    public DbSet<VoiceSample> VoiceSamples => Set<VoiceSample>();
    public DbSet<MeetingMinutes> MeetingMinutes => Set<MeetingMinutes>();
    public DbSet<MinutesSignature> MinutesSignatures => Set<MinutesSignature>();
    public DbSet<MeetingSummary> MeetingSummaries => Set<MeetingSummary>();
    public DbSet<AiProviderSetting> AiProviderSettings => Set<AiProviderSetting>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();
    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();
    public DbSet<CallSession> CallSessions => Set<CallSession>();
    public DbSet<CallParticipant> CallParticipants => Set<CallParticipant>();

    /// <summary>
    /// The audit row written alongside each lifecycle row (BR-445), keyed by the history row, so a retried save does
    /// not write it twice.
    /// </summary>
    private readonly Dictionary<MeetingStatusHistory, AuditLogEntry> _historyAudit = new(ReferenceEqualityComparer.Instance);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        BeforeSave();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        BeforeSave();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void BeforeSave()
    {
        EnsureAppendOnly<MeetingStatusHistory>("Meeting lifecycle history is append-only.");
        // BR-440: no function of the application changes or deletes an audit entry. Change tracker only:
        // ExecuteUpdate/ExecuteDelete and raw SQL bypass it, so never point either at audit_log.
        EnsureAppendOnly<AuditLogEntry>("The audit log is append-only.");
        AuditMeetingHistory();
        StampAuditIpAddress();
    }

    private void EnsureAppendOnly<TEntity>(string message) where TEntity : class
    {
        if (ChangeTracker.Entries<TEntity>().Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// BR-445 — every lifecycle row gets an audit row in the same save, so whichever of the six writers made it (the
    /// overdue job included) the move reaches the audit log, and a move that fails to save leaves neither row. Done here
    /// rather than in each writer because a seventh writer would otherwise forget it.
    /// </summary>
    private void AuditMeetingHistory()
    {
        foreach (var history in ChangeTracker.Entries<MeetingStatusHistory>()
                     .Where(entry => entry.State == EntityState.Added).Select(entry => entry.Entity).ToList())
        {
            if (_historyAudit.TryGetValue(history, out var written) && Entry(written).State != EntityState.Detached)
            {
                continue;
            }

            var at = history.OccurredAt == default ? DateTimeOffset.UtcNow : history.OccurredAt;
            var audit = new AuditLogEntry
            {
                ActorId = history.ActorId,
                Action = AuditActions.MeetingHistory(history.Action),
                ObjectType = AuditActions.MeetingObjectType,
                ObjectId = history.MeetingId,
                Details = JsonSerializer.Serialize(
                    new
                    {
                        historyId = history.Id,
                        fromStatus = history.FromStatus?.ToString(),
                        toStatus = history.ToStatus.ToString(),
                        reason = history.Reason,
                        assignedApproverId = history.AssignedApproverId,
                        previousOrganizerId = history.PreviousOrganizerId,
                        newOrganizerId = history.NewOrganizerId,
                    },
                    AuditService.DetailsOptions),
                OccurredAt = at,
                CreatedAt = at,
                UpdatedAt = at,
            };
            AuditLog.Add(audit);
            _historyAudit[history] = audit;
        }
    }

    /// <summary>An audit row added without an address gets the request's, when there is a request.</summary>
    private void StampAuditIpAddress()
    {
        if (caller is not { IpAddress: { Length: > 0 } ip })
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries<AuditLogEntry>()
                     .Where(entry => entry.State == EntityState.Added && entry.Entity.IpAddress.Length == 0))
        {
            entry.Entity.IpAddress = ip;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Used for User.Email and Role.Name. PostgreSQL compares text case-sensitively, so without citext
        // "Anh@x.vn" and "anh@x.vn" would be two different accounts that both satisfy the unique
        // index — a textbook way to let one person register twice and confuse sign-in.
        modelBuilder.HasPostgresExtension("citext");
        // Accent-insensitive meeting search (SCRUM-148).
        modelBuilder.HasPostgresExtension("unaccent");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrustCabinetDbContext).Assembly);
    }
}
