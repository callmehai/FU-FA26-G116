using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TranscriptSegmentSpeakerAssigned : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "speaker_assigned_at",
                table: "transcript_segments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "speaker_assigned_by_id",
                table: "transcript_segments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_transcript_segments_speaker_assigned_by_id",
                table: "transcript_segments",
                column: "speaker_assigned_by_id");

            migrationBuilder.AddForeignKey(
                name: "fk_transcript_segments_users_speaker_assigned_by_id",
                table: "transcript_segments",
                column: "speaker_assigned_by_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_transcript_segments_users_speaker_assigned_by_id",
                table: "transcript_segments");

            migrationBuilder.DropIndex(
                name: "ix_transcript_segments_speaker_assigned_by_id",
                table: "transcript_segments");

            migrationBuilder.DropColumn(
                name: "speaker_assigned_at",
                table: "transcript_segments");

            migrationBuilder.DropColumn(
                name: "speaker_assigned_by_id",
                table: "transcript_segments");
        }
    }
}
