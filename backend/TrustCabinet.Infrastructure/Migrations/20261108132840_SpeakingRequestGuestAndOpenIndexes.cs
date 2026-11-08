using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SpeakingRequestGuestAndOpenIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "speaking_requests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "guest_invitation_id",
                table: "speaking_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "invited_at",
                table: "speaking_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_speaking_requests_guest_invitation_id",
                table: "speaking_requests",
                column: "guest_invitation_id");

            migrationBuilder.CreateIndex(
                name: "ux_speaking_requests_floor",
                table: "speaking_requests",
                column: "meeting_id",
                unique: true,
                filter: "status IN ('Invited', 'Speaking')");

            migrationBuilder.CreateIndex(
                name: "ux_speaking_requests_open_guest",
                table: "speaking_requests",
                columns: new[] { "meeting_id", "guest_invitation_id" },
                unique: true,
                filter: "guest_invitation_id IS NOT NULL AND status IN ('Queued', 'Invited', 'Speaking')");

            migrationBuilder.CreateIndex(
                name: "ux_speaking_requests_open_user",
                table: "speaking_requests",
                columns: new[] { "meeting_id", "user_id" },
                unique: true,
                filter: "user_id IS NOT NULL AND status IN ('Queued', 'Invited', 'Speaking')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_speaking_requests_one_speaker",
                table: "speaking_requests",
                sql: "(user_id IS NULL) <> (guest_invitation_id IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_speaking_requests_status",
                table: "speaking_requests",
                sql: "status IN ('Queued', 'Invited', 'Speaking', 'Finished', 'Cancelled')");

            migrationBuilder.AddForeignKey(
                name: "fk_speaking_requests_meeting_guest_invitations_guest_invitatio",
                table: "speaking_requests",
                column: "guest_invitation_id",
                principalTable: "meeting_guest_invitations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // user_id goes back to NOT NULL: a guest's request has no user to keep, so rolling back loses it.
            migrationBuilder.Sql("DELETE FROM speaking_requests WHERE user_id IS NULL;");

            migrationBuilder.DropForeignKey(
                name: "fk_speaking_requests_meeting_guest_invitations_guest_invitatio",
                table: "speaking_requests");

            migrationBuilder.DropIndex(
                name: "ix_speaking_requests_guest_invitation_id",
                table: "speaking_requests");

            migrationBuilder.DropIndex(
                name: "ux_speaking_requests_floor",
                table: "speaking_requests");

            migrationBuilder.DropIndex(
                name: "ux_speaking_requests_open_guest",
                table: "speaking_requests");

            migrationBuilder.DropIndex(
                name: "ux_speaking_requests_open_user",
                table: "speaking_requests");

            migrationBuilder.DropCheckConstraint(
                name: "ck_speaking_requests_one_speaker",
                table: "speaking_requests");

            migrationBuilder.DropCheckConstraint(
                name: "ck_speaking_requests_status",
                table: "speaking_requests");

            migrationBuilder.DropColumn(
                name: "guest_invitation_id",
                table: "speaking_requests");

            migrationBuilder.DropColumn(
                name: "invited_at",
                table: "speaking_requests");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "speaking_requests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
