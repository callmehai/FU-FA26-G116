using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <summary>
    /// ADR-0060 — one Voice ID consent covers both sets of samples, so the per-set <c>scope</c> goes. A separate
    /// Secretary-recording consent (ADR-0060) ends here: its samples are erased and it is closed, as a withdrawal would,
    /// because what the person agreed to no longer exists on its own; they agree once, under notice 1.3.
    ///
    /// The built-in Secretary role is also given "Thu giọng mẫu hộ" on databases seeded before it existed, once: the
    /// seeder only fills an empty database, and the demo server should work without an administrator setting it first.
    /// An administrator who later takes it away is not overridden; this runs a single time. An empty
    /// table is left empty, or the seeder (which fills only an empty one) would skip every other capability.
    /// </summary>
    public partial class VoiceIdOneConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM voice_samples s USING voice_consents c
                WHERE s.consent_id = c.id AND c.scope = 'Proxy';
                UPDATE voice_consents SET withdrawn_at = now(), updated_at = now()
                WHERE scope = 'Proxy' AND withdrawn_at IS NULL;
                INSERT INTO role_capabilities (role_id, capability)
                SELECT id, 'VoiceCollector' FROM roles WHERE id = '01a0ce78-7203-7c7f-8326-7a0baebf4c22'
                    AND EXISTS (SELECT 1 FROM role_capabilities)
                ON CONFLICT DO NOTHING;
                """);

            migrationBuilder.DropIndex(
                name: "ix_voice_consents_user_id_scope",
                table: "voice_consents");

            migrationBuilder.DropColumn(
                name: "scope",
                table: "voice_consents");

            migrationBuilder.CreateIndex(
                name: "ix_voice_consents_user_id",
                table: "voice_consents",
                column: "user_id",
                unique: true,
                filter: "withdrawn_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_voice_consents_user_id",
                table: "voice_consents");

            migrationBuilder.AddColumn<string>(
                name: "scope",
                table: "voice_consents",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Self");

            migrationBuilder.CreateIndex(
                name: "ix_voice_consents_user_id_scope",
                table: "voice_consents",
                columns: new[] { "user_id", "scope" },
                unique: true,
                filter: "withdrawn_at IS NULL");
        }
    }
}
