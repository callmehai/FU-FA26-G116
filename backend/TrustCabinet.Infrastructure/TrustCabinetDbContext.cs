using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure;

/// <summary>
/// The application's only database context. Entity shapes live in one configuration class each,
/// under Configurations/, rather than in a growing OnModelCreating.
/// </summary>
public class TrustCabinetDbContext(DbContextOptions<TrustCabinetDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RoleCapability> RoleCapabilities => Set<RoleCapability>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<MeetingParticipant> MeetingParticipants => Set<MeetingParticipant>();
    public DbSet<MeetingGuestInvitation> MeetingGuestInvitations => Set<MeetingGuestInvitation>();
    public DbSet<MeetingInvitationDispatch> MeetingInvitationDispatches => Set<MeetingInvitationDispatch>();
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
    public DbSet<VoteReceipt> VoteReceipts => Set<VoteReceipt>();
    public DbSet<VoteChoice> VoteChoices => Set<VoteChoice>();
    public DbSet<Recording> Recordings => Set<Recording>();
    public DbSet<Transcript> Transcripts => Set<Transcript>();
    public DbSet<TranscriptSegment> TranscriptSegments => Set<TranscriptSegment>();
    public DbSet<MeetingMinutes> MeetingMinutes => Set<MeetingMinutes>();
    public DbSet<MinutesSignature> MinutesSignatures => Set<MinutesSignature>();
    public DbSet<MeetingSummary> MeetingSummaries => Set<MeetingSummary>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();
    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();
    public DbSet<CallSession> CallSessions => Set<CallSession>();
    public DbSet<CallParticipant> CallParticipants => Set<CallParticipant>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureMeetingHistoryIsAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnsureMeetingHistoryIsAppendOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnsureMeetingHistoryIsAppendOnly()
    {
        var mutation = ChangeTracker.Entries<MeetingStatusHistory>()
            .FirstOrDefault(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        if (mutation is not null)
        {
            throw new InvalidOperationException("Meeting lifecycle history is append-only.");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Used for User.Email and Role.Name. PostgreSQL compares text case-sensitively, so without citext
        // "Anh@x.vn" and "anh@x.vn" would be two different accounts that both satisfy the unique
        // index — a textbook way to let one person register twice and confuse sign-in.
        modelBuilder.HasPostgresExtension("citext");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrustCabinetDbContext).Assembly);
    }
}
