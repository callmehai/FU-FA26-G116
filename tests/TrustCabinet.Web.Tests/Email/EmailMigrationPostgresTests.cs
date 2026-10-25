using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure;

namespace TrustCabinet.Web.Tests.Email;

/// <summary>
/// Runs SCRUM-15's migration from its shared predecessor so a developer-only schema history can
/// never become a hidden prerequisite.
/// </summary>
public sealed class EmailMigrationPostgresTests : IAsyncLifetime
{
    private const string BeforeReliableEmailDelivery =
        "20260924153656_NotificationManagement";

    private readonly PostgreSqlTestDatabase _database = new(
        PostgresTestDatabase.ConnectionString("trustcabinet_email_migration_tests"));

    private TrustCabinetDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _db = new TrustCabinetDbContext(
            new DbContextOptionsBuilder<TrustCabinetDbContext>()
                .UseNpgsql(
                    _database.DataSource,
                    npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
                .UseSnakeCaseNamingConvention()
                .Options);

        await MigrateToAsync(BeforeReliableEmailDelivery);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task Migration_preserves_existing_email_jobs_with_distinct_legacy_deduplication_keys()
    {
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO email_messages
                (id, created_at, updated_at, to_address, subject, body, status, attempt_count, last_error)
            VALUES
                ({first}, {now}, {now}, 'first@example.vn', 'Legacy first', 'Body one', 'Queued', 0, ''),
                ({second}, {now}, {now}, 'second@example.vn', 'Legacy second', 'Body two', 'Sent', 1, '')
            """);

        await MigrateToAsync();

        var jobs = await _db.EmailMessages.OrderBy(email => email.ToAddress).ToListAsync();
        Assert.Equal(2, jobs.Count);
        Assert.Equal($"legacy-email:{first:D}", jobs[0].DeduplicationKey);
        Assert.Equal($"legacy-email:{second:D}", jobs[1].DeduplicationKey);
        Assert.Equal("Body one", jobs[0].Body);
        Assert.Equal("Body two", jobs[1].Body);
        Assert.Equal(TrustCabinet.Domain.Enums.EmailStatus.Queued, jobs[0].Status);
        Assert.Equal(TrustCabinet.Domain.Enums.EmailStatus.Sent, jobs[1].Status);
    }

    [Fact]
    public async Task Already_upgraded_databases_keep_delivery_keys_leases_and_job_content()
    {
        await MigrateToAsync("20260930080344_ReliableEmailDelivery");
        // Model the state of main deployed before this feature: ReliableEmailDelivery was
        // applied, but the new preservation migration has not run on that database yet.
        await _db.Database.ExecuteSqlRawAsync("""
            DROP TABLE __email_message_delivery_upgrade;
            DELETE FROM __ef_migrations_history
                WHERE migration_id = '20260930080343_StageLegacyEmailJobsForReliableDelivery';
            """);
        var leaseToken = Guid.CreateVersion7();
        var leaseExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10);
        var job = new TrustCabinet.Domain.Entities.EmailMessage
        {
            DeduplicationKey = "already-reliable:event:recipient",
            ToAddress = "existing@example.vn",
            Subject = "Existing invitation",
            Body = "Content already rendered",
            CalendarAttachment = "BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n",
            Status = TrustCabinet.Domain.Enums.EmailStatus.Processing,
            AttemptCount = 2,
            LeaseToken = leaseToken,
            LeaseExpiresAt = leaseExpiresAt,
        };
        _db.EmailMessages.Add(job);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        await MigrateToAsync();

        var persisted = await _db.EmailMessages.SingleAsync();
        Assert.Equal(job.Id, persisted.Id);
        Assert.Equal(job.DeduplicationKey, persisted.DeduplicationKey);
        Assert.Equal(job.Body, persisted.Body);
        Assert.Equal(job.CalendarAttachment, persisted.CalendarAttachment);
        Assert.Equal(job.Status, persisted.Status);
        Assert.Equal(2, persisted.AttemptCount);
        Assert.Equal(leaseToken, persisted.LeaseToken);
        Assert.Equal(leaseExpiresAt.ToUnixTimeMilliseconds(), persisted.LeaseExpiresAt!.Value.ToUnixTimeMilliseconds());
        Assert.Empty(await _db.Database.SqlQuery<string>($"""
            SELECT table_name AS "Value" FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name = '__email_message_delivery_upgrade'
            """).ToListAsync());
    }

    [Fact]
    public async Task Email_trace_cleanup_preserves_delivery_jobs_and_notification_history_links_and_reverts_schema()
    {
        const string beforeCleanup = "20260930165727_RestoreLegacyEmailJobsWithDeliveryKeys";
        await MigrateToAsync(beforeCleanup);

        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            FullName = "Existing recipient",
            Email = "trace-cleanup@example.vn",
            PasswordHash = "test-only-placeholder",
        };
        var meeting = new Meeting
        {
            Title = "Already published meeting",
            Status = MeetingStatus.Published,
            Sensitivity = Sensitivity.Normal,
            Mode = MeetingMode.InPerson,
            Location = "Existing meeting room",
            CreatedBy = user,
            CreatedById = user.Id,
        };
        var historyId = Guid.CreateVersion7();
        var notificationId = Guid.CreateVersion7();
        const string legacyPayload = """{"legacy":true}""";
        var leaseToken = Guid.CreateVersion7();
        var leaseExpiresAt = now.AddMinutes(10);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO meetings (
                id, title, description, status, sensitivity, mode, location,
                starts_at, ends_at, created_by_id, assigned_approver_id, created_at, updated_at)
            VALUES (
                {meeting.Id}, {meeting.Title}, {string.Empty}, {meeting.Status.ToString()},
                {meeting.Sensitivity.ToString()}, {meeting.Mode.ToString()}, {meeting.Location},
                {now.AddHours(1)}, {now.AddHours(2)}, {user.Id}, {null}, {now}, {now})
            """);
        var job = new EmailMessage
        {
            DeduplicationKey = $"meeting:{meeting.Id:D}:published:{user.Id:D}",
            ToAddress = user.Email,
            Subject = "Saved invitation",
            Body = "Content already rendered",
            CalendarAttachment = "BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n",
            MeetingId = meeting.Id,
            Status = EmailStatus.Processing,
            AttemptCount = 2,
            LeaseToken = leaseToken,
            LeaseExpiresAt = leaseExpiresAt,
        };
        _db.EmailMessages.Add(job);
        await _db.SaveChangesAsync();

        // The previous schema has no organizer-transfer fields. Insert its evidence directly
        // without asking the current EF history model to select columns not added yet.
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO meeting_status_history
                (id, created_at, updated_at, meeting_id, from_status, to_status,
                 action, actor_id, reason, occurred_at)
            VALUES
                ({historyId}, {now}, {now}, {meeting.Id}, 'Approved', 'Published',
                 'Publish', {user.Id}, '', {now});
            INSERT INTO notifications
                (id, created_at, updated_at, user_id, meeting_id, origin_history_id,
                 type, title, body)
            VALUES
                ({notificationId}, {now}, {now}, {user.Id}, {meeting.Id}, {historyId},
                 'MeetingInvited', 'Existing notification', 'Saved notification');
            UPDATE email_messages
                SET origin_history_id = {historyId}, recipient_user_id = {user.Id},
                    message_type = 'MeetingInvitation', payload_snapshot = {legacyPayload}
                WHERE id = {job.Id};
            """);
        _db.ChangeTracker.Clear();

        Assert.Equal(4, (await EmailTraceColumnsAsync()).Length);
        Assert.Equal(2, (await EmailTraceForeignKeysAsync()).Length);

        await MigrateToAsync();

        Assert.Empty(await EmailTraceColumnsAsync());
        Assert.Empty(await EmailTraceForeignKeysAsync());
        var upgradedIndexes = await EmailIndexesAsync();
        Assert.DoesNotContain("ix_email_messages_origin_history_id_recipient_user_id_message_", upgradedIndexes);
        Assert.DoesNotContain("ix_email_messages_recipient_user_id", upgradedIndexes);
        Assert.Contains("ix_email_messages_deduplication_key", upgradedIndexes);
        await AssertJobPreservedAsync();

        var notification = await _db.Notifications.AsNoTracking().SingleAsync();
        Assert.Equal(notificationId, notification.Id);
        Assert.Equal(historyId, notification.OriginHistoryId);
        var history = await _db.MeetingStatusHistory.AsNoTracking().SingleAsync();
        Assert.Equal(historyId, history.Id);
        Assert.Equal(user.Id, history.ActorId);
        Assert.Equal(MeetingHistoryAction.Publish, history.Action);
        Assert.True(await _db.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM pg_constraint
                WHERE conrelid = 'public.notifications'::regclass
                  AND conname = 'fk_notifications_meeting_status_history_origin_history_id'
            ) AND EXISTS (
                SELECT 1 FROM pg_indexes
                WHERE schemaname = 'public' AND tablename = 'notifications'
                  AND indexname = 'ix_notifications_origin_history_id_user_id_type'
            ) AS "Value"
            """).SingleAsync());
        var mutation = await Assert.ThrowsAsync<PostgresException>(() =>
            _db.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE FROM meeting_status_history WHERE id = {historyId}
                """));
        Assert.Equal(PostgresErrorCodes.RaiseException, mutation.SqlState);

        await MigrateToAsync(beforeCleanup);

        Assert.Equal(
            ["message_type", "origin_history_id", "payload_snapshot", "recipient_user_id"],
            await EmailTraceColumnsAsync());
        Assert.Equal(2, (await EmailTraceForeignKeysAsync()).Length);
        Assert.Equal(4, await _db.Database.SqlQuery<int>($"""
            SELECT COUNT(*)::integer AS "Value" FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'email_messages'
              AND column_name IN (
                'message_type', 'origin_history_id', 'payload_snapshot', 'recipient_user_id')
              AND is_nullable = 'YES'
            """).SingleAsync());
        var restoredIndexes = await EmailIndexesAsync();
        Assert.Contains("ix_email_messages_origin_history_id_recipient_user_id_message_", restoredIndexes);
        Assert.Contains("ix_email_messages_recipient_user_id", restoredIndexes);
        await AssertJobPreservedAsync();
        Assert.Equal(historyId, (await _db.Notifications.AsNoTracking().SingleAsync()).OriginHistoryId);

        // Down restores the optional schema, not discarded legacy metadata values.
        Assert.Equal(1, await _db.Database.SqlQuery<int>($"""
            SELECT COUNT(*)::integer AS "Value" FROM email_messages
            WHERE id = {job.Id} AND origin_history_id IS NULL AND recipient_user_id IS NULL
                AND message_type IS NULL AND payload_snapshot IS NULL
            """).SingleAsync());

        async Task AssertJobPreservedAsync()
        {
            var persisted = await _db.EmailMessages.AsNoTracking().SingleAsync();
            Assert.Equal(job.Id, persisted.Id);
            Assert.Equal(job.DeduplicationKey, persisted.DeduplicationKey);
            Assert.Equal(job.ToAddress, persisted.ToAddress);
            Assert.Equal(job.Subject, persisted.Subject);
            Assert.Equal(job.Body, persisted.Body);
            Assert.Equal(job.CalendarAttachment, persisted.CalendarAttachment);
            Assert.Equal(job.MeetingId, persisted.MeetingId);
            Assert.Equal(job.Status, persisted.Status);
            Assert.Equal(job.AttemptCount, persisted.AttemptCount);
            Assert.Equal(leaseToken, persisted.LeaseToken);
            Assert.Equal(leaseExpiresAt.ToUnixTimeMilliseconds(), persisted.LeaseExpiresAt!.Value.ToUnixTimeMilliseconds());
        }
    }

    [Fact]
    public async Task Review_corrections_rollback_refuses_to_erase_organizer_transfer_evidence()
    {
        await MigrateToAsync();
        var reviewMigration = Assert.Single(await _db.Database.GetAppliedMigrationsAsync(),
            id => id.EndsWith("_MeetingWorkflowReviewCorrections", StringComparison.Ordinal));
        var now = DateTimeOffset.UtcNow;
        var previousOrganizer = new User
        {
            FullName = "Former organizer",
            Email = "previous-organizer@example.vn",
            PasswordHash = "test-only-placeholder",
            IsActive = false,
        };
        var newOrganizer = new User
        {
            FullName = "Replacement organizer",
            Email = "new-organizer@example.vn",
            PasswordHash = "test-only-placeholder",
        };
        var administrator = new User
        {
            FullName = "Transfer administrator",
            Email = "transfer-admin@example.vn",
            PasswordHash = "test-only-placeholder",
            RoleId = TrustCabinet.Domain.Access.BuiltInRoles.Admin,
        };
        var meeting = new Meeting
        {
            Title = "Approved meeting after organizer recovery",
            Status = MeetingStatus.Approved,
            Sensitivity = Sensitivity.Normal,
            Mode = MeetingMode.InPerson,
            Location = "Meeting room",
            CreatedBy = newOrganizer,
            CreatedById = newOrganizer.Id,
        };
        var transfer = new MeetingStatusHistory
        {
            MeetingId = meeting.Id,
            FromStatus = MeetingStatus.Approved,
            ToStatus = MeetingStatus.Approved,
            Action = MeetingHistoryAction.TransferOrganizer,
            PreviousOrganizerId = previousOrganizer.Id,
            NewOrganizerId = newOrganizer.Id,
            ActorId = administrator.Id,
            Reason = "Previous organizer account was deactivated.",
            OccurredAt = now,
        };
        _db.Users.AddRange(previousOrganizer, newOrganizer, administrator);
        _db.Meetings.Add(meeting);
        _db.MeetingStatusHistory.Add(transfer);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        // Npgsql's EF execution strategy wraps SQLSTATE 55000 in InvalidOperationException.
        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MigrateToAsync("20260930165727_RestoreLegacyEmailJobsWithDeliveryKeys"));

        var postgres = Assert.IsType<PostgresException>(refusal.InnerException);
        Assert.Equal(PostgresErrorCodes.ObjectNotInPrerequisiteState, postgres.SqlState);
        Assert.Contains(reviewMigration, await _db.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await EmailTraceColumnsAsync());
        var persisted = await _db.MeetingStatusHistory.AsNoTracking().SingleAsync();
        Assert.Equal(transfer.Id, persisted.Id);
        Assert.Equal(MeetingHistoryAction.TransferOrganizer, persisted.Action);
        Assert.Equal(previousOrganizer.Id, persisted.PreviousOrganizerId);
        Assert.Equal(newOrganizer.Id, persisted.NewOrganizerId);
        Assert.Equal(administrator.Id, persisted.ActorId);
        Assert.Equal(transfer.Reason, persisted.Reason);
        Assert.Equal(now.ToUnixTimeMilliseconds(), persisted.OccurredAt.ToUnixTimeMilliseconds());
        // Read the column directly: the refused rollback already undid later meeting columns.
        Assert.Equal(newOrganizer.Id, await _db.Database.SqlQuery<Guid>($"""
            SELECT created_by_id AS "Value" FROM meetings
            """).SingleAsync());
        Assert.Equal(2, await _db.Database.SqlQuery<int>($"""
            SELECT COUNT(*)::integer AS "Value" FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'meeting_status_history'
              AND column_name IN ('previous_organizer_id', 'new_organizer_id')
            """).SingleAsync());
        Assert.Equal(2, await _db.Database.SqlQuery<int>($"""
            SELECT COUNT(*)::integer AS "Value" FROM pg_constraint
            WHERE conrelid = 'public.meeting_status_history'::regclass
              AND conname IN (
                'fk_meeting_status_history_users_previous_organizer_id',
                'fk_meeting_status_history_users_new_organizer_id')
            """).SingleAsync());
        Assert.Equal(2, await _db.Database.SqlQuery<int>($"""
            SELECT COUNT(*)::integer AS "Value" FROM pg_indexes
            WHERE schemaname = 'public' AND tablename = 'meeting_status_history'
              AND indexname IN (
                'ix_meeting_status_history_previous_organizer_id',
                'ix_meeting_status_history_new_organizer_id')
            """).SingleAsync());
    }

    [Fact]
    public async Task Migration_applies_and_reverts_from_the_shared_predecessor()
    {
        await MigrateToAsync();

        Assert.Equal(
            ["deduplication_key", "lease_expires_at", "lease_token", "next_attempt_at"],
            await DeliveryColumnsAsync());
        Assert.Contains(
            "ix_email_messages_deduplication_key",
            await EmailIndexesAsync());

        await MigrateToAsync(BeforeReliableEmailDelivery);

        Assert.Empty(await DeliveryColumnsAsync());
        Assert.Contains("ix_email_messages_status_created_at", await EmailIndexesAsync());
    }

    private Task MigrateToAsync(string? target = null)
        => _db.GetService<IMigrator>().MigrateAsync(target ?? _db.Database.GetMigrations().TakeWhile(m => !m.EndsWith("SynchronizeAgendaWithPoRequirements", StringComparison.Ordinal)).Last());

    private async Task<string[]> DeliveryColumnsAsync()
        => await _db.Database.SqlQuery<string>($"""
                SELECT column_name AS "Value"
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = 'email_messages'
                  AND column_name IN (
                    'deduplication_key',
                    'lease_expires_at',
                    'lease_token',
                    'next_attempt_at')
                ORDER BY column_name
                """)
            .ToArrayAsync();

    private async Task<string[]> EmailIndexesAsync()
        => await _db.Database.SqlQuery<string>($"""
                SELECT indexname AS "Value"
                FROM pg_indexes
                WHERE schemaname = 'public'
                  AND tablename = 'email_messages'
                ORDER BY indexname
                """)
            .ToArrayAsync();

    private async Task<string[]> EmailTraceColumnsAsync()
        => await _db.Database.SqlQuery<string>($"""
                SELECT column_name AS "Value" FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'email_messages'
                  AND column_name IN (
                    'message_type', 'origin_history_id', 'payload_snapshot', 'recipient_user_id')
                ORDER BY column_name
                """)
            .ToArrayAsync();

    private async Task<string[]> EmailTraceForeignKeysAsync()
        => await _db.Database.SqlQuery<string>($"""
                SELECT conname AS "Value" FROM pg_constraint
                WHERE conrelid = 'public.email_messages'::regclass
                  AND conname IN (
                    'fk_email_messages_meeting_status_history_origin_history_id',
                    'fk_email_messages_users_recipient_user_id')
                ORDER BY conname
                """)
            .ToArrayAsync();
}
