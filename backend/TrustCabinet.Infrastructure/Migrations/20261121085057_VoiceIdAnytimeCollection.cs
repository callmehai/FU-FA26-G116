using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VoiceIdAnytimeCollection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_voice_samples_collected_in_meeting",
                table: "voice_samples");

            migrationBuilder.AddCheckConstraint(
                name: "ck_voice_samples_meeting_has_collector",
                table: "voice_samples",
                sql: "meeting_id IS NULL OR collected_by_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_voice_samples_meeting_has_collector",
                table: "voice_samples");

            // The old rule has no place for a sample taken outside a meeting (ADR-0071): it goes, as a withdrawal would.
            migrationBuilder.Sql("DELETE FROM voice_samples WHERE collected_by_id IS NOT NULL AND meeting_id IS NULL;");

            migrationBuilder.AddCheckConstraint(
                name: "ck_voice_samples_collected_in_meeting",
                table: "voice_samples",
                sql: "(collected_by_id IS NULL) = (meeting_id IS NULL)");
        }
    }
}
