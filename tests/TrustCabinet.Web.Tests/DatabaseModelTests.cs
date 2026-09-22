using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure;
using TrustCabinet.Infrastructure.Seed;

namespace TrustCabinet.Web.Tests;

/// <summary>
/// Checks the EF model without a database: building the model catches a bad configuration, and
/// these assertions pin the decisions a later change might quietly undo.
/// </summary>
public class DatabaseModelTests
{
    private static TrustCabinetDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseNpgsql("Host=none;Database=none")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new TrustCabinetDbContext(options);
    }

    [Fact]
    public void Model_builds_with_the_expected_tables()
    {
        using var db = NewContext();

        var tables = db.Model.GetEntityTypes()
            .Select(e => e.GetTableName()!)
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToArray();

        // Spelled out rather than counted: adding an entity should be a visible edit here, and
        // this is also what catches one that was written but never registered.
        Assert.Equal(
            [
                "agenda_items", "audit_log", "call_participants", "call_sessions",
                "document_comments", "document_versions", "documents", "email_messages",
                "meeting_minutes", "meeting_notes", "meeting_participants",
                "meeting_status_history", "meeting_summaries", "meetings", "minutes_signatures",
                "notifications", "recordings", "role_capabilities", "speaking_requests",
                "transcript_segments", "transcripts", "users", "vote_choices",
                "vote_receipts", "voting_options", "voting_sessions",
            ],
            tables);
    }

    [Theory]
    [InlineData(typeof(Meeting), nameof(Meeting.Status))]
    [InlineData(typeof(Meeting), nameof(Meeting.Sensitivity))]
    [InlineData(typeof(User), nameof(User.Role))]
    public void Enums_are_stored_as_text(Type entity, string property)
    {
        using var db = NewContext();

        var column = db.Model.FindEntityType(entity)!.FindProperty(property)!;

        // A readable database is worth the few extra bytes, and inserting a new enum member later
        // must not renumber the rows already written.
        Assert.Equal(typeof(string), column.GetProviderClrType());
    }

    [Fact]
    public void Email_is_case_insensitive()
    {
        using var db = NewContext();

        var email = db.Model.FindEntityType(typeof(User))!.FindProperty(nameof(User.Email))!;

        // PostgreSQL compares text case-sensitively. Without citext the unique index below would
        // happily accept "Anh@x.vn" alongside "anh@x.vn" as two separate accounts.
        Assert.Equal("citext", email.GetColumnType());
    }

    [Fact]
    public void Email_is_unique()
    {
        using var db = NewContext();

        var indexes = db.Model.FindEntityType(typeof(User))!.GetIndexes();

        Assert.Contains(indexes, i =>
            i.IsUnique && i.Properties.Count == 1 && i.Properties[0].Name == nameof(User.Email));
    }

    [Fact]
    public void An_account_may_sign_in_with_a_password_or_with_google()
    {
        using var db = NewContext();

        var user = db.Model.FindEntityType(typeof(User))!;

        // Both nullable: a Google-only account has no password, and a password-only account has no
        // Google link. Making either required would make one of the two sign-in methods
        // unstorable — which is exactly the bug SCRUM-11's migration had to fix.
        Assert.True(user.FindProperty(nameof(User.PasswordHash))!.IsNullable);
        Assert.True(user.FindProperty(nameof(User.GoogleSubjectId))!.IsNullable);

        // But not both null, or the row is an account nobody could ever sign into. Enforced by the
        // database because migrations, seeds and a DBA's console all write here too.
        //
        // Read from the design-time model: the runtime one is trimmed of anything queries do not
        // need, and check constraints — which only ever matter when generating a migration — are
        // among what it drops.
        var designTimeUser = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(User))!;

        Assert.Contains(
            designTimeUser.GetCheckConstraints(),
            c => c.Sql.Contains("password_hash IS NOT NULL")
                && c.Sql.Contains("google_subject_id IS NOT NULL"));
    }

    [Fact]
    public void One_google_identity_belongs_to_at_most_one_account()
    {
        using var db = NewContext();

        var indexes = db.Model.FindEntityType(typeof(User))!.GetIndexes();

        // Without this, one Google identity could be linked to two internal accounts and which one
        // signed in would come down to row order.
        Assert.Contains(indexes, i => i.IsUnique
            && i.Properties.Count == 1
            && i.Properties[0].Name == nameof(User.GoogleSubjectId));
    }

    [Fact]
    public void Role_capabilities_match_the_agreed_default_mapping()
    {
        // These are the values a **fresh** database starts with, not the running system's answer.
        // Since SCRUM-60 an administrator edits the matrix at runtime, so this test pins the
        // default that ReferenceDataSeeder applies to an empty table — read role_capabilities for
        // what a given deployment actually enforces.
        //
        // Still spelled out rather than derived, so changing who may approve a meeting has to be a
        // deliberate edit in two places instead of a silent one.
        var expected = new[]
        {
            (SystemRole.Chair, Capability.MeetingCreator),
            (SystemRole.Chair, Capability.MeetingApprover),
            (SystemRole.Secretary, Capability.MeetingCreator),
            (SystemRole.Secretary, Capability.MinutesEditor),
            (SystemRole.Admin, Capability.AdminUsers),
        };

        var actual = ReferenceData.RoleCapabilities
            .Select(rc => (rc.Role, rc.Capability))
            .ToArray();

        Assert.Equal(expected, actual);

        // A plain Member attends, reads and votes — none of which needs a capability.
        Assert.DoesNotContain(actual, rc => rc.Role == SystemRole.Member);
    }

    [Fact]
    public void Default_matrix_grants_admin_users_to_someone()
    {
        // A default matrix where nobody administers would produce a database no one can sign in
        // to configure — the state RolePermissionService refuses to be edited into, so it must
        // not be shipped as the starting point either.
        Assert.Contains(
            ReferenceData.RoleCapabilities, rc => rc.Capability == Capability.AdminUsers);
    }

    [Fact]
    public void Role_capabilities_are_not_seeded_through_the_migration()
    {
        using var db = NewContext();

        // HasData would put the matrix inside the migration, and EF diffs seed data against the
        // previous migration rather than against the database — so the next migration would emit
        // UPDATE/DELETE over exactly the rows an administrator changed on the running system,
        // silently restoring the defaults on deploy (ADR-0007, SCRUM-60).
        //
        // The rows now come from ReferenceDataSeeder, which only fills an empty table.
        //
        // Read from the design-time model: seed data is what migrations are scaffolded against,
        // and the runtime model deliberately drops it.
        var seeded = db.GetService<IDesignTimeModel>().Model
            .FindEntityType(typeof(RoleCapability))!
            .GetSeedData();

        Assert.Empty(seeded);
    }

    [Fact]
    public void Columns_are_snake_case()
    {
        using var db = NewContext();

        var column = db.Model.FindEntityType(typeof(Meeting))!
            .FindProperty(nameof(Meeting.StartsAt))!
            .GetColumnName();

        Assert.Equal("starts_at", column);
    }

    [Fact]
    public void Deleting_a_meeting_takes_its_participants_and_history_with_it()
    {
        using var db = NewContext();

        foreach (var (entity, navigation) in new[]
                 {
                     (typeof(MeetingParticipant), nameof(MeetingParticipant.Meeting)),
                     (typeof(MeetingStatusHistory), nameof(MeetingStatusHistory.Meeting)),
                 })
        {
            var foreignKey = db.Model.FindEntityType(entity)!
                .GetForeignKeys()
                .Single(fk => fk.DependentToPrincipal?.Name == navigation);

            Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
        }
    }

    [Fact]
    public void An_anonymous_vote_cannot_be_traced_to_its_voter()
    {
        using var db = NewContext();

        var voter = db.Model.FindEntityType(typeof(VoteChoice))!.FindProperty(nameof(VoteChoice.VoterId))!;

        // The column has to be nullable for a secret ballot to be possible at all: in an anonymous
        // session the application leaves it null, so no row links a person to what they chose.
        // Making it required would force every ballot to name its voter and quietly turn every
        // "secret" vote into an open one.
        Assert.True(voter.IsNullable);
    }

    [Fact]
    public void One_person_gets_one_vote_per_session()
    {
        using var db = NewContext();

        var indexes = db.Model.FindEntityType(typeof(VoteReceipt))!.GetIndexes();

        // Enforced by the database, because two simultaneous requests can both pass an
        // application-level "has this person voted?" check.
        Assert.Contains(indexes, i => i.IsUnique
            && i.Properties.Select(p => p.Name).OrderBy(n => n)
                .SequenceEqual(new[] { nameof(VoteReceipt.VotingSessionId), nameof(VoteReceipt.VoterId) }.OrderBy(n => n)));
    }

    [Fact]
    public void Nobody_belongs_to_an_organisational_unit()
    {
        using var db = NewContext();

        var suspicious = db.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties().Select(p => $"{e.ClrType.Name}.{p.Name}"))
            .Where(name => name.Contains("OrgUnit", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Department", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        // TrustCabinet serves one organisation with no internal hierarchy: there is no unit to
        // belong to, to own a meeting, or to invite wholesale. The cut is deliberate (ADR-0004),
        // and this is here so it survives someone adding "just one column" back.
        Assert.Empty(suspicious);
    }

    [Fact]
    public void No_voiceprint_is_stored_anywhere()
    {
        using var db = NewContext();

        var suspicious = db.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties().Select(p => $"{e.ClrType.Name}.{p.Name}"))
            .Where(name => name.Contains("Voiceprint", StringComparison.OrdinalIgnoreCase)
                || name.Contains("VoiceEmbedding", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Biometric", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        // Speaker separation within one recording is in scope; recognising the same voice across
        // meetings is not. A voiceprint is biometric data under Decree 13/2023/NĐ-CP, with its own
        // consent and erasure duties, and the project cut it deliberately. This test is here so
        // the cut survives someone adding "just one column".
        Assert.Empty(suspicious);
    }

    [Fact]
    public void A_recording_records_its_consent_and_retention()
    {
        using var db = NewContext();

        var recording = db.Model.FindEntityType(typeof(Recording))!;

        // Decree 13/2023/NĐ-CP: consent before capture, a stated purpose, and erasure. The schema
        // has to be able to show each of those, not just the audio file.
        foreach (var column in new[]
                 {
                     nameof(Recording.ConsentObtainedAt),
                     nameof(Recording.ConsentPurpose),
                     nameof(Recording.RetainUntil),
                     nameof(Recording.DeletedAt),
                 })
        {
            Assert.NotNull(recording.FindProperty(column));
        }
    }

    [Fact]
    public void Issued_minutes_are_corrected_by_a_new_revision()
    {
        using var db = NewContext();

        var minutes = db.Model.FindEntityType(typeof(MeetingMinutes))!;

        // One row per revision, so the text that was actually issued stays readable after a
        // correction instead of being overwritten by it.
        Assert.Contains(minutes.GetIndexes(), i => i.IsUnique
            && i.Properties.Select(p => p.Name).OrderBy(n => n)
                .SequenceEqual(new[] { nameof(MeetingMinutes.MeetingId), nameof(MeetingMinutes.RevisionNumber) }.OrderBy(n => n)));

        Assert.Equal(DeleteBehavior.Restrict, minutes
            .GetForeignKeys()
            .Single(fk => fk.DependentToPrincipal?.Name == nameof(MeetingMinutes.Supersedes))
            .DeleteBehavior);
    }

    [Fact]
    public void Audit_details_are_queryable_json()
    {
        using var db = NewContext();

        var details = db.Model.FindEntityType(typeof(AuditLogEntry))!.FindProperty(nameof(AuditLogEntry.Details))!;

        // jsonb rather than text, so a question the columns do not answer can still be asked of
        // the payload without rewriting history.
        Assert.Equal("jsonb", details.GetColumnType());
    }
}
