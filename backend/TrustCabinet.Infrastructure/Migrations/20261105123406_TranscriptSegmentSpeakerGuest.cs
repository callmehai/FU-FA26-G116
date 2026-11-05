using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TranscriptSegmentSpeakerGuest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "speaker_guest_id",
                table: "transcript_segments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_transcript_segments_speaker_guest_id",
                table: "transcript_segments",
                column: "speaker_guest_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_transcript_segments_one_speaker",
                table: "transcript_segments",
                sql: "speaker_user_id IS NULL OR speaker_guest_id IS NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_transcript_segments_meeting_guest_invitations_speaker_guest",
                table: "transcript_segments",
                column: "speaker_guest_id",
                principalTable: "meeting_guest_invitations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_transcript_segments_meeting_guest_invitations_speaker_guest",
                table: "transcript_segments");

            migrationBuilder.DropIndex(
                name: "ix_transcript_segments_speaker_guest_id",
                table: "transcript_segments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_transcript_segments_one_speaker",
                table: "transcript_segments");

            migrationBuilder.DropColumn(
                name: "speaker_guest_id",
                table: "transcript_segments");
        }
    }
}
