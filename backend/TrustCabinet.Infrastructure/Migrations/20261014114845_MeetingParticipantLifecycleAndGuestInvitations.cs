using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MeetingParticipantLifecycleAndGuestInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Unsupported historical identities need an explicit mapping, never silent deletion.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM meeting_participants WHERE role = 'Guest' OR response = 'Delegated' OR delegated_to_user_id IS NOT NULL) THEN
                        RAISE EXCEPTION 'SCRUM-20 requires a reviewed mapping for legacy Guest/delegated participant data before upgrade.';
                    END IF;
                    IF EXISTS (SELECT meeting_id FROM meeting_participants WHERE role = 'Chair' GROUP BY meeting_id HAVING count(*) > 1) THEN
                        RAISE EXCEPTION 'SCRUM-20 requires resolving multiple current Chairs before upgrade; no data was removed.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "ix_meeting_participants_meeting_id_user_id",
                table: "meeting_participants");

            migrationBuilder.AddColumn<long>(
                name: "participants_revision",
                table: "meetings",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "assignment_status",
                table: "meeting_participants",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Current");

            migrationBuilder.AddColumn<string>(
                name: "initial_invitation_delivery_key",
                table: "meeting_participants",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "removed_at",
                table: "meeting_participants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "removed_by_id",
                table: "meeting_participants",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE meeting_participants
                SET initial_invitation_delivery_key = 'meeting:' || meeting_id::text || ':published:' || user_id::text;
                UPDATE meeting_participants p SET role = 'Chair'
                FROM meetings m WHERE p.meeting_id = m.id AND p.user_id = m.created_by_id
                    AND p.assignment_status = 'Current'
                    AND NOT EXISTS (SELECT 1 FROM meeting_participants c
                        WHERE c.meeting_id = m.id AND c.assignment_status = 'Current' AND c.role = 'Chair');
                INSERT INTO meeting_participants
                    (id, meeting_id, user_id, role, assignment_status, response, attendance,
                     initial_invitation_delivery_key, created_at, updated_at)
                SELECT gen_random_uuid(), m.id, m.created_by_id, 'Chair', 'Current', 'Pending', 'Unknown',
                    'meeting:' || m.id::text || ':published:' || m.created_by_id::text, now(), now()
                FROM meetings m WHERE NOT EXISTS (SELECT 1 FROM meeting_participants c
                    WHERE c.meeting_id = m.id AND c.assignment_status = 'Current' AND c.role = 'Chair');
                """);

            migrationBuilder.CreateTable(
                name: "meeting_guest_invitations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    meeting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invited_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "citext", maxLength: 256, nullable: false),
                    invited_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sensitivity_at_invitation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    response = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    attendance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Unknown"),
                    attendance_recorded_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    attendance_recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    initial_invitation_delivery_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting_guest_invitations", x => x.id);
                    table.CheckConstraint("ck_guest_revocation", "(revoked_at IS NULL) = (revoked_by_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_meeting_guest_invitations_meetings_meeting_id",
                        column: x => x.meeting_id,
                        principalTable: "meetings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_meeting_guest_invitations_users_attendance_recorded_by_id",
                        column: x => x.attendance_recorded_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_meeting_guest_invitations_users_invited_by_id",
                        column: x => x.invited_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_meeting_guest_invitations_users_revoked_by_id",
                        column: x => x.revoked_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "meeting_invitation_dispatches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    meeting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guest_invitation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    deduplication_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    payload_version = table.Column<int>(type: "integer", nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    dependency_attempt_count = table.Column<int>(type: "integer", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    email_message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting_invitation_dispatches", x => x.id);
                    table.CheckConstraint("ck_dispatch_owner", "(participant_id IS NULL) <> (guest_invitation_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_meeting_invitation_dispatches_email_messages_email_message_",
                        column: x => x.email_message_id,
                        principalTable: "email_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_meeting_invitation_dispatches_meeting_guest_invitations_gue",
                        column: x => x.guest_invitation_id,
                        principalTable: "meeting_guest_invitations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_meeting_invitation_dispatches_meeting_participants_particip",
                        column: x => x.participant_id,
                        principalTable: "meeting_participants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_meeting_invitation_dispatches_meetings_meeting_id",
                        column: x => x.meeting_id,
                        principalTable: "meetings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_participants_revision",
                table: "meetings",
                sql: "participants_revision >= 0");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_participants_meeting_id_assignment_status",
                table: "meeting_participants",
                columns: new[] { "meeting_id", "assignment_status" });

            migrationBuilder.CreateIndex(
                name: "ix_meeting_participants_meeting_id_user_id",
                table: "meeting_participants",
                columns: new[] { "meeting_id", "user_id" },
                unique: true,
                filter: "assignment_status = 'Current'");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_participants_removed_by_id",
                table: "meeting_participants",
                column: "removed_by_id");

            migrationBuilder.CreateIndex(
                name: "ux_current_meeting_chair",
                table: "meeting_participants",
                column: "meeting_id",
                unique: true,
                filter: "assignment_status = 'Current' AND role = 'Chair'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_participant_removal",
                table: "meeting_participants",
                sql: "(assignment_status = 'Current' AND removed_at IS NULL AND removed_by_id IS NULL) OR (assignment_status = 'Removed' AND removed_at IS NOT NULL AND removed_by_id IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_guest_invitations_attendance_recorded_by_id",
                table: "meeting_guest_invitations",
                column: "attendance_recorded_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_guest_invitations_invited_by_id",
                table: "meeting_guest_invitations",
                column: "invited_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_guest_invitations_meeting_id_email",
                table: "meeting_guest_invitations",
                columns: new[] { "meeting_id", "email" },
                unique: true,
                filter: "revoked_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_guest_invitations_revoked_by_id",
                table: "meeting_guest_invitations",
                column: "revoked_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_invitation_dispatches_deduplication_key",
                table: "meeting_invitation_dispatches",
                column: "deduplication_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_meeting_invitation_dispatches_email_message_id",
                table: "meeting_invitation_dispatches",
                column: "email_message_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_invitation_dispatches_guest_invitation_id",
                table: "meeting_invitation_dispatches",
                column: "guest_invitation_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_invitation_dispatches_meeting_id",
                table: "meeting_invitation_dispatches",
                column: "meeting_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_invitation_dispatches_participant_id",
                table: "meeting_invitation_dispatches",
                column: "participant_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_invitation_dispatches_state_next_attempt_at",
                table: "meeting_invitation_dispatches",
                columns: new[] { "state", "next_attempt_at" });

            migrationBuilder.AddForeignKey(
                name: "fk_meeting_participants_users_removed_by_id",
                table: "meeting_participants",
                column: "removed_by_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM meeting_guest_invitations)
                        OR EXISTS (SELECT 1 FROM meeting_invitation_dispatches)
                        OR EXISTS (SELECT 1 FROM meeting_participants WHERE assignment_status = 'Removed') THEN
                        RAISE EXCEPTION 'SCRUM-20 rollback would discard invitation/history data. Use a reviewed forward fix or backup restore.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_meeting_participants_users_removed_by_id",
                table: "meeting_participants");

            migrationBuilder.DropTable(
                name: "meeting_invitation_dispatches");

            migrationBuilder.DropTable(
                name: "meeting_guest_invitations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_participants_revision",
                table: "meetings");

            migrationBuilder.DropIndex(
                name: "ix_meeting_participants_meeting_id_assignment_status",
                table: "meeting_participants");

            migrationBuilder.DropIndex(
                name: "ix_meeting_participants_meeting_id_user_id",
                table: "meeting_participants");

            migrationBuilder.DropIndex(
                name: "ix_meeting_participants_removed_by_id",
                table: "meeting_participants");

            migrationBuilder.DropIndex(
                name: "ux_current_meeting_chair",
                table: "meeting_participants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_participant_removal",
                table: "meeting_participants");

            migrationBuilder.DropColumn(
                name: "participants_revision",
                table: "meetings");

            migrationBuilder.DropColumn(
                name: "assignment_status",
                table: "meeting_participants");

            migrationBuilder.DropColumn(
                name: "initial_invitation_delivery_key",
                table: "meeting_participants");

            migrationBuilder.DropColumn(
                name: "removed_at",
                table: "meeting_participants");

            migrationBuilder.DropColumn(
                name: "removed_by_id",
                table: "meeting_participants");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_participants_meeting_id_user_id",
                table: "meeting_participants",
                columns: new[] { "meeting_id", "user_id" },
                unique: true);
        }
    }
}
