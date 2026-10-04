using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MeetingWorkflowReviewCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_email_messages_meeting_status_history_origin_history_id",
                table: "email_messages");

            migrationBuilder.DropForeignKey(
                name: "fk_email_messages_users_recipient_user_id",
                table: "email_messages");

            migrationBuilder.DropIndex(
                name: "ix_email_messages_origin_history_id_recipient_user_id_message_",
                table: "email_messages");

            migrationBuilder.DropIndex(
                name: "ix_email_messages_recipient_user_id",
                table: "email_messages");

            migrationBuilder.DropColumn(
                name: "message_type",
                table: "email_messages");

            migrationBuilder.DropColumn(
                name: "origin_history_id",
                table: "email_messages");

            migrationBuilder.DropColumn(
                name: "payload_snapshot",
                table: "email_messages");

            migrationBuilder.DropColumn(
                name: "recipient_user_id",
                table: "email_messages");

            migrationBuilder.AddColumn<Guid>(
                name: "new_organizer_id",
                table: "meeting_status_history",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "previous_organizer_id",
                table: "meeting_status_history",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_meeting_status_history_new_organizer_id",
                table: "meeting_status_history",
                column: "new_organizer_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_status_history_previous_organizer_id",
                table: "meeting_status_history",
                column: "previous_organizer_id");

            migrationBuilder.AddForeignKey(
                name: "fk_meeting_status_history_users_new_organizer_id",
                table: "meeting_status_history",
                column: "new_organizer_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_meeting_status_history_users_previous_organizer_id",
                table: "meeting_status_history",
                column: "previous_organizer_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Do not erase organizer-transfer evidence or leave an action the old model cannot read.
            migrationBuilder.Sql("""
                LOCK TABLE meeting_status_history IN ACCESS EXCLUSIVE MODE;
                DO $block$
                BEGIN
                    IF EXISTS (SELECT 1 FROM meeting_status_history WHERE action = 'TransferOrganizer') THEN
                        RAISE EXCEPTION 'Cannot downgrade after organizer transfers: lifecycle history must remain intact.'
                            USING ERRCODE = '55000';
                    END IF;
                END
                $block$;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_meeting_status_history_users_new_organizer_id",
                table: "meeting_status_history");

            migrationBuilder.DropForeignKey(
                name: "fk_meeting_status_history_users_previous_organizer_id",
                table: "meeting_status_history");

            migrationBuilder.DropIndex(
                name: "ix_meeting_status_history_new_organizer_id",
                table: "meeting_status_history");

            migrationBuilder.DropIndex(
                name: "ix_meeting_status_history_previous_organizer_id",
                table: "meeting_status_history");

            migrationBuilder.DropColumn(
                name: "new_organizer_id",
                table: "meeting_status_history");

            migrationBuilder.DropColumn(
                name: "previous_organizer_id",
                table: "meeting_status_history");

            migrationBuilder.AddColumn<string>(
                name: "message_type",
                table: "email_messages",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "origin_history_id",
                table: "email_messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payload_snapshot",
                table: "email_messages",
                type: "character varying(50000)",
                maxLength: 50000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "recipient_user_id",
                table: "email_messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_email_messages_origin_history_id_recipient_user_id_message_",
                table: "email_messages",
                columns: new[] { "origin_history_id", "recipient_user_id", "message_type" },
                unique: true,
                filter: "origin_history_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_email_messages_recipient_user_id",
                table: "email_messages",
                column: "recipient_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_email_messages_meeting_status_history_origin_history_id",
                table: "email_messages",
                column: "origin_history_id",
                principalTable: "meeting_status_history",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_email_messages_users_recipient_user_id",
                table: "email_messages",
                column: "recipient_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
