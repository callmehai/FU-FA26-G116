using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveMeetingScheduleToSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM meetings AS m
                        WHERE NOT EXISTS (
                            SELECT 1 FROM meeting_sessions AS s WHERE s.meeting_id = m.id)
                          AND (m.starts_at IS NULL
                               OR m.starts_at = '-infinity'::timestamptz
                               OR m.starts_at <= TIMESTAMPTZ '0001-01-01 00:00:00+00')) THEN
                        RAISE EXCEPTION 'Cannot move meeting schedules: a meeting without sessions has no valid starts_at.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM meetings AS m
                        WHERE NOT EXISTS (
                            SELECT 1 FROM meeting_sessions AS s WHERE s.meeting_id = m.id)
                          AND m.ends_at IS NOT NULL
                          AND m.ends_at <= m.starts_at) THEN
                        RAISE EXCEPTION 'Cannot move meeting schedules: a meeting without sessions has ends_at before or equal to starts_at.';
                    END IF;
                END $$;

                INSERT INTO meeting_sessions (
                    id,
                    meeting_id,
                    title,
                    planned_starts_at,
                    planned_ends_at,
                    status,
                    created_at,
                    updated_at)
                SELECT
                    (
                        substring(generated.uuid_hex FROM 1 FOR 8) || '-' ||
                        substring(generated.uuid_hex FROM 9 FOR 4) || '-' ||
                        substring(generated.uuid_hex FROM 13 FOR 4) || '-' ||
                        substring(generated.uuid_hex FROM 17 FOR 4) || '-' ||
                        substring(generated.uuid_hex FROM 21 FOR 12)
                    )::uuid,
                    m.id,
                    m.title,
                    m.starts_at,
                    COALESCE(m.ends_at, m.starts_at + INTERVAL '1 hour'),
                    CASE
                        WHEN m.status IN ('Draft', 'WaitingApproval', 'Rejected', 'Approved', 'Published')
                            THEN 'Planned'
                        WHEN m.status = 'InProgress' THEN 'InProgress'
                        WHEN m.status IN ('Ended', 'MinutesIssued', 'Archived') THEN 'Completed'
                        ELSE 'Planned'
                    END,
                    m.created_at,
                    m.updated_at
                FROM meetings AS m
                CROSS JOIN LATERAL (
                    SELECT
                        lpad(to_hex(floor(extract(epoch FROM clock_timestamp()) * 1000)::bigint), 12, '0') ||
                        '7' ||
                        substring(entropy.random_hex FROM 1 FOR 3) ||
                        '8' ||
                        substring(entropy.random_hex FROM 4 FOR 15) AS uuid_hex
                    FROM (
                        SELECT md5(m.id::text || clock_timestamp()::text || random()::text) AS random_hex
                    ) AS entropy
                ) AS generated
                WHERE NOT EXISTS (
                    SELECT 1 FROM meeting_sessions AS s WHERE s.meeting_id = m.id);
                """);

            migrationBuilder.DropIndex(
                name: "ix_meetings_starts_at",
                table: "meetings");

            migrationBuilder.DropIndex(
                name: "ix_meetings_status_starts_at",
                table: "meetings");

            migrationBuilder.DropColumn(
                name: "ends_at",
                table: "meetings");

            migrationBuilder.DropColumn(
                name: "starts_at",
                table: "meetings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ends_at",
                table: "meetings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "starts_at",
                table: "meetings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE meetings AS m
                SET starts_at = COALESCE((
                    SELECT s.planned_starts_at
                    FROM meeting_sessions AS s
                    WHERE s.meeting_id = m.id AND s.status <> 'Cancelled'
                    ORDER BY s.planned_starts_at, s.id
                    LIMIT 1), m.created_at),
                    ends_at = COALESCE((
                    SELECT s.planned_ends_at
                    FROM meeting_sessions AS s
                    WHERE s.meeting_id = m.id AND s.status <> 'Cancelled'
                    ORDER BY s.planned_starts_at, s.id
                    LIMIT 1), m.created_at + INTERVAL '1 hour');
                """);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "starts_at",
                table: "meetings",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_meetings_starts_at",
                table: "meetings",
                column: "starts_at");

            migrationBuilder.CreateIndex(
                name: "ix_meetings_status_starts_at",
                table: "meetings",
                columns: new[] { "status", "starts_at" });
        }
    }
}
