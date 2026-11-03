using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GuestSpeechClips : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_speech_clips_meeting_id_speaker_id_client_clip_id",
                table: "speech_clips");

            migrationBuilder.AlterColumn<Guid>(
                name: "speaker_id",
                table: "speech_clips",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "guest_invitation_id",
                table: "speech_clips",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_speech_clips_guest_invitation_id",
                table: "speech_clips",
                column: "guest_invitation_id");

            migrationBuilder.CreateIndex(
                name: "ix_speech_clips_meeting_id_guest_invitation_id_client_clip_id",
                table: "speech_clips",
                columns: new[] { "meeting_id", "guest_invitation_id", "client_clip_id" },
                unique: true,
                filter: "guest_invitation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_speech_clips_meeting_id_speaker_id_client_clip_id",
                table: "speech_clips",
                columns: new[] { "meeting_id", "speaker_id", "client_clip_id" },
                unique: true,
                filter: "speaker_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_speech_clips_one_speaker",
                table: "speech_clips",
                sql: "(speaker_id IS NULL) <> (guest_invitation_id IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "fk_speech_clips_meeting_guest_invitations_guest_invitation_id",
                table: "speech_clips",
                column: "guest_invitation_id",
                principalTable: "meeting_guest_invitations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A guest's sentence has no speaker_id to fall back to, and deleting it here would drop personal data and leave
            // its audio file in storage; once a guest has spoken, restore the backup taken before the deploy instead.
            migrationBuilder.Sql(
                "DO $$ BEGIN IF EXISTS (SELECT 1 FROM speech_clips WHERE guest_invitation_id IS NOT NULL) THEN " +
                "RAISE EXCEPTION 'GuestSpeechClips cannot be reverted: guests have spoken. Restore the backup taken before the deploy.'; " +
                "END IF; END $$;");

            migrationBuilder.DropForeignKey(
                name: "fk_speech_clips_meeting_guest_invitations_guest_invitation_id",
                table: "speech_clips");

            migrationBuilder.DropIndex(
                name: "ix_speech_clips_guest_invitation_id",
                table: "speech_clips");

            migrationBuilder.DropIndex(
                name: "ix_speech_clips_meeting_id_guest_invitation_id_client_clip_id",
                table: "speech_clips");

            migrationBuilder.DropIndex(
                name: "ix_speech_clips_meeting_id_speaker_id_client_clip_id",
                table: "speech_clips");

            migrationBuilder.DropCheckConstraint(
                name: "ck_speech_clips_one_speaker",
                table: "speech_clips");

            migrationBuilder.DropColumn(
                name: "guest_invitation_id",
                table: "speech_clips");

            migrationBuilder.AlterColumn<Guid>(
                name: "speaker_id",
                table: "speech_clips",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_speech_clips_meeting_id_speaker_id_client_clip_id",
                table: "speech_clips",
                columns: new[] { "meeting_id", "speaker_id", "client_clip_id" },
                unique: true);
        }
    }
}
