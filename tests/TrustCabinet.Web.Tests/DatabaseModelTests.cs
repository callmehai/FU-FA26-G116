using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Access;
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
                "document_comments", "document_read_confirmations", "documents", "email_messages", "folders",
                "meeting_document_agenda_items", "meeting_documents", "meeting_guest_invitations", "meeting_invitation_dispatches",
                "meeting_minutes", "meeting_notes", "meeting_participants",
                "meeting_status_history", "meeting_summaries", "meetings", "minutes_signatures",
                "notifications", "recordings", "role_capabilities", "roles", "speaking_requests", "speech_clips",
                "stored_files",
                "transcript_segments", "transcript_speakers", "transcripts", "users", "voice_consents",
                "voice_samples", "vote_choices", "vote_documents", "vote_eligible_voters", "vote_receipts",
                "voting_options", "voting_sessions",
            ],
            tables);
    }

    [Theory]
    [InlineData(typeof(Meeting), nameof(Meeting.Status))]
    [InlineData(typeof(Meeting), nameof(Meeting.Sensitivity))]
    [InlineData(typeof(RoleCapability), nameof(RoleCapability.Capability))]
    public void Enums_are_stored_as_text(Type entity, string property)
    {
        using var db = NewContext();

        var column = db.Model.FindEntityType(entity)!.FindProperty(property)!;

        // A readable database is worth the few extra bytes, and inserting a new enum member later
        // must not renumber the rows already written.
        Assert.Equal(typeof(string), column.GetProviderClrType());
    }

    [Fact]
    public async Task Meeting_history_rejects_update_and_delete_through_ef()
    {
        var updateContext = NewContext();
        var history = new MeetingStatusHistory
        {
            MeetingId = Guid.CreateVersion7(),
            ActorId = Guid.CreateVersion7(),
            Action = MeetingHistoryAction.Approve,
            ToStatus = MeetingStatus.Approved,
            OccurredAt = DateTimeOffset.UtcNow,
        };
        updateContext.Attach(history);
        updateContext.Entry(history).State = EntityState.Modified;

        Assert.Throws<InvalidOperationException>(() => updateContext.SaveChanges());
        await updateContext.DisposeAsync();

        var deleteContext = NewContext();
        deleteContext.Attach(history);
        deleteContext.Entry(history).State = EntityState.Deleted;

        await Assert.ThrowsAsync<InvalidOperationException>(() => deleteContext.SaveChangesAsync());
        await deleteContext.DisposeAsync();
    }

    [Fact]
    public void Meeting_history_action_is_required_and_meeting_delete_is_restricted()
    {
        using var db = NewContext();
        var history = db.Model.FindEntityType(typeof(MeetingStatusHistory))!;
        var action = history.FindProperty(nameof(MeetingStatusHistory.Action))!;
        var meetingForeignKey = history.GetForeignKeys()
            .Single(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(Meeting));

        Assert.False(action.IsNullable);
        Assert.Equal(typeof(string), action.GetProviderClrType());
        Assert.Equal(DeleteBehavior.Restrict, meetingForeignKey.DeleteBehavior);
    }

    [Fact]
    public void Meeting_carries_its_own_required_schedule_indexed_for_the_list()
    {
        using var db = NewContext();
        var meeting = db.Model.FindEntityType(typeof(Meeting))!;

        // ADR-0013: one meeting is one sitting, so the schedule lives on the meeting itself.
        Assert.False(meeting.FindProperty(nameof(Meeting.PlannedStartTime))!.IsNullable);
        Assert.False(meeting.FindProperty(nameof(Meeting.PlannedEndTime))!.IsNullable);
        Assert.True(meeting.FindProperty(nameof(Meeting.ActualStartedAt))!.IsNullable);
        Assert.True(meeting.FindProperty(nameof(Meeting.ActualEndedAt))!.IsNullable);
        Assert.True(meeting.FindProperty(nameof(Meeting.OnlineLink))!.IsNullable);
        Assert.Equal(1000, meeting.FindProperty(nameof(Meeting.OnlineLink))!.GetMaxLength());
        Assert.Contains(meeting.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(Meeting.Status), nameof(Meeting.PlannedStartTime)]));
        Assert.Null(db.Model.GetEntityTypes().SingleOrDefault(type => type.ClrType.Name == "MeetingSession"));
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
    public void Role_names_are_unique_regardless_of_case()
    {
        using var db = NewContext();

        var role = db.Model.FindEntityType(typeof(Role))!;

        // citext + a unique index: the database itself refuses "thư ký" beside "Thư ký", which is
        // what stops two administrators creating the same role at once (SCRUM-63).
        Assert.Equal("citext", role.FindProperty(nameof(Role.Name))!.GetColumnType());
        Assert.Contains(role.GetIndexes(), i =>
            i.IsUnique && i.Properties.Count == 1 && i.Properties[0].Name == nameof(Role.Name));
    }

    [Fact]
    public void A_role_somebody_holds_cannot_be_deleted_but_its_capabilities_go_with_it()
    {
        using var db = NewContext();

        // RESTRICT from users: the last line behind MSG25 — an account assigned between the
        // service's check and the delete makes the database refuse, rather than leaving that
        // account pointing at nothing.
        var holder = db.Model.FindEntityType(typeof(User))!
            .GetForeignKeys()
            .Single(fk => fk.PrincipalEntityType.ClrType == typeof(Role));

        Assert.Equal(DeleteBehavior.Restrict, holder.DeleteBehavior);
        Assert.Equal(nameof(User.RoleId), holder.Properties.Single().Name);

        // CASCADE from role_capabilities: those rows are the role's own data.
        var grants = db.Model.FindEntityType(typeof(RoleCapability))!
            .GetForeignKeys()
            .Single(fk => fk.PrincipalEntityType.ClrType == typeof(Role));

        Assert.Equal(DeleteBehavior.Cascade, grants.DeleteBehavior);
    }

    [Fact]
    public void Roles_are_not_seeded_through_the_model()
    {
        using var db = NewContext();

        // The built-in roles are inserted by the RolesAsData migration and topped up by
        // ReferenceDataSeeder. HasData would make every later migration re-assert them — the
        // problem ADR-0007 describes for role_capabilities.
        var seeded = db.GetService<IDesignTimeModel>().Model
            .FindEntityType(typeof(Role))!
            .GetSeedData();

        Assert.Empty(seeded);
    }

    [Fact]
    public void Built_in_roles_have_the_fixed_ids_in_display_order()
    {
        // The ids are pasted in once and shared by the migration, the seeder and every database.
        var roles = ReferenceData.Roles();

        Assert.Equal(BuiltInRoles.All, roles.Select(r => r.Id));
        Assert.Equal(
            ["Chủ toạ", "Thư ký", "Thành viên", "Quản trị viên"],
            roles.Select(r => r.Name));
        Assert.All(roles, r => Assert.True(r.IsBuiltIn));
        Assert.Equal(BuiltInRoles.All.Count, BuiltInRoles.All.Distinct().Count());
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
            (BuiltInRoles.Chair, Capability.MeetingCreator),
            (BuiltInRoles.Chair, Capability.MeetingApprover),
            (BuiltInRoles.Secretary, Capability.MeetingCreator),
            (BuiltInRoles.Secretary, Capability.MinutesEditor),
            (BuiltInRoles.Admin, Capability.AdminUsers),
        };

        var actual = ReferenceData.RoleCapabilities
            .Select(rc => (rc.RoleId, rc.Capability))
            .ToArray();

        Assert.Equal(expected, actual);

        // A plain Member attends, reads and votes — none of which needs a capability.
        Assert.DoesNotContain(actual, rc => rc.RoleId == BuiltInRoles.Member);
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
    public void Meeting_schedule_columns_are_not_mapped()
    {
        using var db = NewContext();

        var meeting = db.Model.FindEntityType(typeof(Meeting))!;

        Assert.Null(meeting.FindProperty("StartsAt"));
        Assert.Null(meeting.FindProperty("EndsAt"));
    }

    [Fact]
    public void Deleting_a_meeting_cascades_participants_but_preserves_lifecycle_history()
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

            Assert.Equal(
                entity == typeof(MeetingParticipant) ? DeleteBehavior.Cascade : DeleteBehavior.Restrict,
                foreignKey.DeleteBehavior);
        }
    }

    [Fact]
    public void An_anonymous_vote_cannot_be_traced_to_its_voter()
    {
        using var db = NewContext();

        // A secret ballot stores no choice row at all: even one naming nobody comes back from a
        // plain SELECT in the order the receipts were written, which pairs it with its voter
        // (review of PR #200). So every choice must name its voter, and a secret ballot is only
        // counted on its option.
        var check = Assert.Single(db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(VoteChoice))!.GetCheckConstraints(),
            c => c.Name == "ck_vote_choices_one_voter");
        Assert.Equal("num_nonnulls(voter_id, guest_invitation_id) = 1", check.Sql);
        Assert.NotNull(db.Model.FindEntityType(typeof(VotingOption))!.FindProperty(nameof(VotingOption.BallotCount)));
        Assert.Null(db.Model.FindEntityType(typeof(VoteChoice))!.FindProperty("CastAt"));
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
    public void A_guest_votes_once_by_invitation_not_as_a_user()
    {
        using var db = NewContext();

        // ADR-0016: Guests vote, but never have a User row, so a ballot names its voter by
        // invitation — and one-person-one-vote has to hold for that key as well.
        foreach (var entity in new[] { typeof(VoteReceipt), typeof(VoteEligibleVoter) })
        {
            var type = db.Model.FindEntityType(entity)!;
            Assert.Contains(type.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(MeetingGuestInvitation));
            Assert.Contains(type.GetIndexes(), i => i.IsUnique
                && i.Properties.Select(p => p.Name).Contains("GuestInvitationId")
                && i.Properties.Select(p => p.Name).Contains(nameof(VoteReceipt.VotingSessionId)));
        }
    }

    [Fact]
    public void A_vote_belongs_to_the_meeting_not_to_an_agenda_item()
    {
        using var db = NewContext();

        // BR-285 / ADR-0014: a link to an agenda item would invite screens and minutes to group
        // votes under items, which the PO ruled out.
        Assert.DoesNotContain(
            db.Model.FindEntityType(typeof(VotingSession))!.GetForeignKeys(),
            fk => fk.PrincipalEntityType.ClrType == typeof(AgendaItem));
    }

    [Fact]
    public void A_meeting_has_at_most_one_open_vote()
    {
        using var db = NewContext();

        // BR-266, enforced by the database so two simultaneous Open requests cannot both succeed.
        Assert.Contains(
            db.Model.FindEntityType(typeof(VotingSession))!.GetIndexes(),
            i => i.IsUnique
                && i.Properties.Single().Name == nameof(VotingSession.MeetingId)
                && i.GetFilter() == "status = 'Open'");
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
    public void Every_enum_column_default_is_a_name_the_enum_knows()
    {
        using var db = NewContext();

        // Enums are stored as strings (AGENTS.md), so a column default is only usable if it spells
        // a member of that enum. Anything else is a value the application can write but cannot
        // read back: the conversion runs Enum.Parse on the way out, so a bad default surfaces as a
        // 500 at some later read rather than an error at the insert that produced it.
        //
        // This is how SCRUM-61 happened. `attendance` was added NOT NULL to a table that already
        // had rows, and EF supplied `DEFAULT ''` on its own — nobody wrote that default, which is
        // exactly why a test has to look for it rather than a reviewer.
        var offenders = new List<string>();

        foreach (var entity in db.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                var enumType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;

                if (!enumType.IsEnum || property.GetDefaultValue() is not { } declared)
                {
                    continue;
                }

                // The default is read in whatever shape it was declared: a string once the value
                // converter has been applied, or the enum member itself when set through
                // HasDefaultValue(). Both have to name something the enum defines.
                var valid = declared switch
                {
                    string text => Enum.GetNames(enumType).Contains(text, StringComparer.Ordinal),
                    _ => declared.GetType() == enumType && Enum.IsDefined(enumType, declared),
                };

                if (!valid)
                {
                    offenders.Add(
                        $"{entity.ClrType.Name}.{property.Name} ({enumType.Name}) "
                        + $"defaults to '{declared}'");
                }
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void Attendance_defaults_to_Unknown_rather_than_an_empty_string()
    {
        using var db = NewContext();

        var attendance = db.Model
            .FindEntityType(typeof(MeetingParticipant))!
            .GetProperty(nameof(MeetingParticipant.Attendance));

        // The specific column SCRUM-61 was raised for. The sweep above would catch a regression
        // here too, but this one names the column, so a failure says what broke without anyone
        // having to reconstruct the story.
        //
        // The default matters even on a clean database: it is what a hand-written INSERT that
        // omits the column lands on, and `''` there would be unreadable.
        //
        // Compared as the enum member, which is how HasDefaultValue() stored it — the value
        // converter turns it into 'Unknown' on the way to SQL. Asserting against the string would
        // fail here while the schema was perfectly correct.
        Assert.Equal(AttendanceStatus.Unknown, attendance.GetDefaultValue());
    }


    [Fact]
    public void A_voice_is_stored_only_as_an_encrypted_enrolment_sample()
    {
        using var db = NewContext();

        // Voice ID is in scope since ADR-0027, and a voice used to recognise someone is biometric data under
        // Luật BVDLCN 91/2025/QH15. The only place it may live is VoiceSample.Ciphertext, under a consent that can
        // be withdrawn. A float array or a column named after an embedding anywhere else — on a transcript, a
        // speaker, a user — would be a voiceprint kept without that consent, so this test fails on one.
        var suspicious = db.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties().Select(p => (Entity: e.ClrType, Property: p)))
            .Where(x => x.Property.ClrType == typeof(float[]) || x.Property.ClrType == typeof(double[])
                || x.Property.Name.Contains("Embedding", StringComparison.OrdinalIgnoreCase) && x.Entity != typeof(VoiceSample)
                || x.Property.Name.Contains("Voiceprint", StringComparison.OrdinalIgnoreCase)
                || x.Property.Name.Contains("Biometric", StringComparison.OrdinalIgnoreCase))
            .Select(x => $"{x.Entity.Name}.{x.Property.Name}")
            .ToArray();
        Assert.Empty(suspicious);

        var sample = db.Model.FindEntityType(typeof(VoiceSample))!;
        Assert.Equal(typeof(byte[]), sample.FindProperty(nameof(VoiceSample.Ciphertext))!.ClrType);
        Assert.Equal(typeof(string), sample.FindProperty(nameof(VoiceSample.EmbeddingModel))!.ClrType);
        // A sample cannot outlive the consent it was taken under.
        Assert.Equal(DeleteBehavior.Cascade, sample.GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(VoiceConsent)).DeleteBehavior);
        var consent = db.Model.FindEntityType(typeof(VoiceConsent))!;
        Assert.Contains(consent.GetIndexes(), i => i.IsUnique && i.GetFilter() == "withdrawn_at IS NULL");
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
