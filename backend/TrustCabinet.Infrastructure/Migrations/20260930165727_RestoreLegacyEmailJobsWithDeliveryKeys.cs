using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations;

/// <summary>Restores each preserved job without changing an existing reliable-delivery key.</summary>
[DbContext(typeof(TrustCabinetDbContext))]
[Migration("20260930165727_RestoreLegacyEmailJobsWithDeliveryKeys")]
public sealed class RestoreLegacyEmailJobsWithDeliveryKeys : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF to_regclass('public.__email_message_delivery_upgrade') IS NOT NULL THEN
                    INSERT INTO email_messages
                        SELECT (jsonb_populate_record(NULL::email_messages, payload ||
                            jsonb_build_object('deduplication_key',
                                COALESCE(NULLIF(payload ->> 'deduplication_key', ''),
                                    'legacy-email:' || (payload ->> 'id'))))).*
                        FROM __email_message_delivery_upgrade;
                    DROP TABLE __email_message_delivery_upgrade;
                END IF;
            END;
            $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Keep restored rows in place. ReliableEmailDelivery.Down removes the added columns;
        // moving jobs back into a buffer would hide them when targeting that migration.
    }
}
