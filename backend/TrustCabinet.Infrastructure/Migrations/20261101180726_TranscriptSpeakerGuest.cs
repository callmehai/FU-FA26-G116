using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TranscriptSpeakerGuest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "assigned_guest_id",
                table: "transcript_speakers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_transcript_speakers_assigned_guest_id",
                table: "transcript_speakers",
                column: "assigned_guest_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_transcript_speakers_one_assignee",
                table: "transcript_speakers",
                sql: "assigned_user_id IS NULL OR assigned_guest_id IS NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_transcript_speakers_meeting_guest_invitations_assigned_gues",
                table: "transcript_speakers",
                column: "assigned_guest_id",
                principalTable: "meeting_guest_invitations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_transcript_speakers_meeting_guest_invitations_assigned_gues",
                table: "transcript_speakers");

            migrationBuilder.DropIndex(
                name: "ix_transcript_speakers_assigned_guest_id",
                table: "transcript_speakers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_transcript_speakers_one_assignee",
                table: "transcript_speakers");

            migrationBuilder.DropColumn(
                name: "assigned_guest_id",
                table: "transcript_speakers");
        }
    }
}
