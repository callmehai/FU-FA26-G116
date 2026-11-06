using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <summary>
    /// SCRUM-35 — a transcript line is said by its voice only: the Secretary moves a misattributed line to another voice
    /// (a new one for a person no voice is named as), as AI-eCabinet does, so the per-line person column goes. No code
    /// ever wrote it, so nothing is lost; Down brings the empty column back.
    /// </summary>
    public partial class DropTranscriptSegmentSpeakerUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_transcript_segments_users_speaker_user_id",
                table: "transcript_segments");

            migrationBuilder.DropIndex(
                name: "ix_transcript_segments_speaker_user_id",
                table: "transcript_segments");

            migrationBuilder.DropColumn(
                name: "speaker_user_id",
                table: "transcript_segments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "speaker_user_id",
                table: "transcript_segments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_transcript_segments_speaker_user_id",
                table: "transcript_segments",
                column: "speaker_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_transcript_segments_users_speaker_user_id",
                table: "transcript_segments",
                column: "speaker_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
