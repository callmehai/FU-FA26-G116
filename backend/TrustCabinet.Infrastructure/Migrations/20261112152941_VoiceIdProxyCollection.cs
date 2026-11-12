using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VoiceIdProxyCollection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_voice_consents_user_id",
                table: "voice_consents");

            migrationBuilder.AddColumn<Guid>(
                name: "collected_by_id",
                table: "voice_samples",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "meeting_id",
                table: "voice_samples",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "scope",
                table: "voice_consents",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                // Every consent given before ADR-0060 was to enrol one's own voice.
                defaultValue: "Self");

            migrationBuilder.CreateIndex(
                name: "ix_voice_samples_collected_by_id",
                table: "voice_samples",
                column: "collected_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_voice_samples_meeting_id",
                table: "voice_samples",
                column: "meeting_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_voice_samples_collected_in_meeting",
                table: "voice_samples",
                sql: "(collected_by_id IS NULL) = (meeting_id IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_voice_consents_user_id_scope",
                table: "voice_consents",
                columns: new[] { "user_id", "scope" },
                unique: true,
                filter: "withdrawn_at IS NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_voice_samples_meetings_meeting_id",
                table: "voice_samples",
                column: "meeting_id",
                principalTable: "meetings",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_voice_samples_users_collected_by_id",
                table: "voice_samples",
                column: "collected_by_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_voice_samples_meetings_meeting_id",
                table: "voice_samples");

            migrationBuilder.DropForeignKey(
                name: "fk_voice_samples_users_collected_by_id",
                table: "voice_samples");

            migrationBuilder.DropIndex(
                name: "ix_voice_samples_collected_by_id",
                table: "voice_samples");

            migrationBuilder.DropIndex(
                name: "ix_voice_samples_meeting_id",
                table: "voice_samples");

            migrationBuilder.DropCheckConstraint(
                name: "ck_voice_samples_collected_in_meeting",
                table: "voice_samples");

            migrationBuilder.DropIndex(
                name: "ix_voice_consents_user_id_scope",
                table: "voice_consents");

            migrationBuilder.DropColumn(
                name: "collected_by_id",
                table: "voice_samples");

            migrationBuilder.DropColumn(
                name: "meeting_id",
                table: "voice_samples");

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
    }
}
