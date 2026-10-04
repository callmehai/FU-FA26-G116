using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations;

/// <summary>Preserves existing jobs before the immutable SCRUM-15 migration adds a required key.</summary>
[DbContext(typeof(TrustCabinetDbContext))]
[Migration("20260930080343_StageLegacyEmailJobsForReliableDelivery")]
public sealed class StageLegacyEmailJobsForReliableDelivery : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // This deliberately sorts immediately before ReliableEmailDelivery. That accepted
        // migration cannot add its NOT NULL column while legacy email rows are present.
        // PostgreSQL keeps the buffer and restoration inside the migration transaction.
        migrationBuilder.Sql("""
            LOCK TABLE email_messages IN ACCESS EXCLUSIVE MODE;
            CREATE TABLE __email_message_delivery_upgrade (payload jsonb NOT NULL);
            INSERT INTO __email_message_delivery_upgrade (payload)
                SELECT to_jsonb(message) FROM email_messages AS message;
            DELETE FROM email_messages;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Also recover if a developer deliberately stopped at this intermediate migration.
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF to_regclass('public.__email_message_delivery_upgrade') IS NOT NULL THEN
                    INSERT INTO email_messages
                        SELECT (jsonb_populate_record(NULL::email_messages, payload)).*
                        FROM __email_message_delivery_upgrade;
                    DROP TABLE __email_message_delivery_upgrade;
                END IF;
            END;
            $$;
            """);
    }
}
