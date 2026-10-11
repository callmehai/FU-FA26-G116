using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <summary>
    /// ADR-0013: one meeting is one sitting. The schedule moves back from meeting_sessions onto
    /// meetings, the table is dropped, and the meeting lifecycle stops at Ended.
    /// </summary>
    public partial class MeetingOwnsScheduleDropSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "planned_start_time",
                table: "meetings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "planned_end_time",
                table: "meetings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "actual_started_at",
                table: "meetings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "actual_ended_at",
                table: "meetings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "online_link",
                table: "meetings",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            // Earliest start and latest end of the sessions that were not cancelled; a meeting whose
            // sessions were all cancelled keeps their span, and one without any falls back to its
            // creation time. Actual start/end stay null: no session row recorded them as evidence.
            migrationBuilder.Sql(
                """
                UPDATE meetings AS m
                SET planned_start_time = s.starts_at,
                    planned_end_time = s.ends_at
                FROM (
                    SELECT meeting_id,
                           COALESCE(MIN(planned_starts_at) FILTER (WHERE status <> 'Cancelled'),
                                    MIN(planned_starts_at)) AS starts_at,
                           COALESCE(MAX(planned_ends_at) FILTER (WHERE status <> 'Cancelled'),
                                    MAX(planned_ends_at)) AS ends_at
                    FROM meeting_sessions
                    GROUP BY meeting_id
                ) AS s
                WHERE s.meeting_id = m.id;

                UPDATE meetings
                SET planned_start_time = created_at,
                    planned_end_time = created_at + INTERVAL '1 hour'
                WHERE planned_start_time IS NULL;
                """);

            // MinutesIssued and Archived left the meeting lifecycle; the minutes carry their own
            // status. History rows are relabelled too, or the enum could no longer read them back.
            // The append-only trigger is lifted for this one statement and restored in the same
            // transaction: the rows keep their actor, time and action, only the retired label changes.
            migrationBuilder.Sql(
                """
                UPDATE meetings SET status = 'Ended' WHERE status IN ('MinutesIssued', 'Archived');

                ALTER TABLE meeting_status_history DISABLE TRIGGER meeting_status_history_no_update_delete;
                UPDATE meeting_status_history
                SET from_status = CASE WHEN from_status IN ('MinutesIssued', 'Archived') THEN 'Ended' ELSE from_status END,
                    to_status = CASE WHEN to_status IN ('MinutesIssued', 'Archived') THEN 'Ended' ELSE to_status END
                WHERE from_status IN ('MinutesIssued', 'Archived') OR to_status IN ('MinutesIssued', 'Archived');
                ALTER TABLE meeting_status_history ENABLE TRIGGER meeting_status_history_no_update_delete;
                """);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "planned_start_time",
                table: "meetings",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "planned_end_time",
                table: "meetings",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.DropTable(
                name: "meeting_sessions");

            migrationBuilder.CreateIndex(
                name: "ix_meetings_planned_start_time",
                table: "meetings",
                column: "planned_start_time");

            migrationBuilder.CreateIndex(
                name: "ix_meetings_status_planned_start_time",
                table: "meetings",
                columns: new[] { "status", "planned_start_time" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_meetings_planned_start_time",
                table: "meetings");

            migrationBuilder.DropIndex(
                name: "ix_meetings_status_planned_start_time",
                table: "meetings");

            migrationBuilder.CreateTable(
                name: "meeting_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    meeting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    planned_ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    planned_starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_meeting_sessions_meetings_meeting_id",
                        column: x => x.meeting_id,
                        principalTable: "meetings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_meeting_sessions_meeting_id_status",
                table: "meeting_sessions",
                columns: new[] { "meeting_id", "status" });

            // One session per meeting carries its schedule back. MinutesIssued/Archived are not restored.
            migrationBuilder.Sql(
                """
                INSERT INTO meeting_sessions (
                    id, meeting_id, title, planned_starts_at, planned_ends_at, status, created_at, updated_at)
                SELECT gen_random_uuid(), m.id, m.title, m.planned_start_time, m.planned_end_time,
                       CASE m.status
                           WHEN 'InProgress' THEN 'InProgress'
                           WHEN 'Ended' THEN 'Completed'
                           ELSE 'Planned'
                       END,
                       m.created_at, m.updated_at
                FROM meetings AS m;
                """);

            migrationBuilder.DropColumn(
                name: "actual_ended_at",
                table: "meetings");

            migrationBuilder.DropColumn(
                name: "actual_started_at",
                table: "meetings");

            migrationBuilder.DropColumn(
                name: "online_link",
                table: "meetings");

            migrationBuilder.DropColumn(
                name: "planned_end_time",
                table: "meetings");

            migrationBuilder.DropColumn(
                name: "planned_start_time",
                table: "meetings");
        }
    }
}
