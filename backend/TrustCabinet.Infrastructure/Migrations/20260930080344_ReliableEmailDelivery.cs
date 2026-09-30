using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReliableEmailDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Some development databases applied an earlier, removed EmailDeliveryOutbox
            // migration. IF EXISTS/IF NOT EXISTS keeps this migration valid for both those
            // databases and a clean installation from the migrations currently in the repo.
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_email_messages_meeting_id;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_email_messages_status_created_at;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_email_messages_meeting_id_to_address;");

            migrationBuilder.AlterColumn<string>(
                name: "to_address",
                table: "email_messages",
                type: "citext",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.Sql(
                "ALTER TABLE email_messages ADD COLUMN IF NOT EXISTS "
                + "lease_expires_at timestamp with time zone;");

            migrationBuilder.AddColumn<Guid>(
                name: "lease_token",
                table: "email_messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                "ALTER TABLE email_messages ADD COLUMN IF NOT EXISTS "
                + "next_attempt_at timestamp with time zone;");
            migrationBuilder.Sql(
                "ALTER TABLE email_messages ADD COLUMN IF NOT EXISTS "
                + "deduplication_key character varying(512);");
            migrationBuilder.Sql(
                "UPDATE email_messages SET deduplication_key = 'legacy:' || id::text "
                + "WHERE deduplication_key IS NULL;");
            migrationBuilder.Sql(
                "ALTER TABLE email_messages ALTER COLUMN deduplication_key SET NOT NULL;");

            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_email_messages_lease_expires_at "
                + "ON email_messages (lease_expires_at);");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX IF NOT EXISTS ix_email_messages_deduplication_key "
                + "ON email_messages (deduplication_key);");
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_email_messages_meeting_id "
                + "ON email_messages (meeting_id);");
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_email_messages_status_next_attempt_at_created_at "
                + "ON email_messages (status, next_attempt_at, created_at);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_email_messages_lease_expires_at;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_email_messages_deduplication_key;");
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS ix_email_messages_status_next_attempt_at_created_at;");

            migrationBuilder.DropColumn(
                name: "lease_token",
                table: "email_messages");

            migrationBuilder.DropColumn(
                name: "deduplication_key",
                table: "email_messages");

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM __ef_migrations_history
                        WHERE migration_id = '20260924144819_EmailDeliveryOutbox'
                    ) THEN
                        ALTER TABLE email_messages DROP COLUMN IF EXISTS lease_expires_at;
                        ALTER TABLE email_messages DROP COLUMN IF EXISTS next_attempt_at;
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "to_address",
                table: "email_messages",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "citext",
                oldMaxLength: 256);

            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_email_messages_meeting_id "
                + "ON email_messages (meeting_id);");
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_email_messages_status_created_at "
                + "ON email_messages (status, created_at);");
        }
    }
}
