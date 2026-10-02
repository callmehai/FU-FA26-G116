using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MeetingWorkflowHistoryAndInvitationSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_meeting_status_history_meetings_meeting_id",
                table: "meeting_status_history");

            migrationBuilder.AddColumn<Guid>(
                name: "origin_history_id",
                table: "notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "action",
                table: "meeting_status_history",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "assigned_approver_id",
                table: "meeting_status_history",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE meeting_status_history
                SET action = CASE
                    WHEN from_status IS NULL AND to_status = 'Draft' THEN 'Create'
                    WHEN from_status = 'Draft' AND to_status = 'WaitingApproval' THEN 'SubmitForApproval'
                    WHEN from_status = 'WaitingApproval' AND to_status = 'Approved' THEN 'Approve'
                    WHEN from_status = 'WaitingApproval' AND to_status = 'Rejected' THEN 'Reject'
                    WHEN from_status = 'Rejected' AND to_status = 'Draft' THEN 'ReturnToDraft'
                    WHEN from_status = 'Approved' AND to_status = 'Published' THEN 'Publish'
                    ELSE 'LegacyTransition'
                END;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "action",
                table: "meeting_status_history",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

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
                name: "ix_notifications_origin_history_id_user_id_type",
                table: "notifications",
                columns: new[] { "origin_history_id", "user_id", "type" },
                unique: true,
                filter: "origin_history_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_status_history_assigned_approver_id",
                table: "meeting_status_history",
                column: "assigned_approver_id");

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

            migrationBuilder.AddForeignKey(
                name: "fk_meeting_status_history_meetings_meeting_id",
                table: "meeting_status_history",
                column: "meeting_id",
                principalTable: "meetings",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_meeting_status_history_users_assigned_approver_id",
                table: "meeting_status_history",
                column: "assigned_approver_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION prevent_meeting_status_history_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION 'meeting_status_history is append-only';
                END;
                $$;

                CREATE TRIGGER meeting_status_history_no_update_delete
                BEFORE UPDATE OR DELETE ON meeting_status_history
                FOR EACH ROW EXECUTE FUNCTION prevent_meeting_status_history_mutation();

                CREATE TRIGGER meeting_status_history_no_truncate
                BEFORE TRUNCATE ON meeting_status_history
                FOR EACH STATEMENT EXECUTE FUNCTION prevent_meeting_status_history_mutation();
                """);

            migrationBuilder.AddForeignKey(
                name: "fk_notifications_meeting_status_history_origin_history_id",
                table: "notifications",
                column: "origin_history_id",
                principalTable: "meeting_status_history",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS meeting_status_history_no_truncate ON meeting_status_history;
                DROP TRIGGER IF EXISTS meeting_status_history_no_update_delete ON meeting_status_history;
                DROP FUNCTION IF EXISTS prevent_meeting_status_history_mutation();
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_email_messages_meeting_status_history_origin_history_id",
                table: "email_messages");

            migrationBuilder.DropForeignKey(
                name: "fk_email_messages_users_recipient_user_id",
                table: "email_messages");

            migrationBuilder.DropForeignKey(
                name: "fk_meeting_status_history_meetings_meeting_id",
                table: "meeting_status_history");

            migrationBuilder.DropForeignKey(
                name: "fk_meeting_status_history_users_assigned_approver_id",
                table: "meeting_status_history");

            migrationBuilder.DropForeignKey(
                name: "fk_notifications_meeting_status_history_origin_history_id",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_notifications_origin_history_id_user_id_type",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_meeting_status_history_assigned_approver_id",
                table: "meeting_status_history");

            migrationBuilder.DropIndex(
                name: "ix_email_messages_origin_history_id_recipient_user_id_message_",
                table: "email_messages");

            migrationBuilder.DropIndex(
                name: "ix_email_messages_recipient_user_id",
                table: "email_messages");

            migrationBuilder.DropColumn(
                name: "origin_history_id",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "action",
                table: "meeting_status_history");

            migrationBuilder.DropColumn(
                name: "assigned_approver_id",
                table: "meeting_status_history");

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

            migrationBuilder.AddForeignKey(
                name: "fk_meeting_status_history_meetings_meeting_id",
                table: "meeting_status_history",
                column: "meeting_id",
                principalTable: "meetings",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
