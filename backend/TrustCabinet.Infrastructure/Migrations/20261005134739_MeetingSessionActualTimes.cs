using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MeetingSessionActualTimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "actual_ended_at",
                table: "meeting_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "actual_started_at",
                table: "meeting_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_meeting_sessions_meeting_id_in_progress",
                table: "meeting_sessions",
                column: "meeting_id",
                unique: true,
                filter: "status = 'InProgress'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_meeting_sessions_meeting_id_in_progress",
                table: "meeting_sessions");

            migrationBuilder.DropColumn(
                name: "actual_ended_at",
                table: "meeting_sessions");

            migrationBuilder.DropColumn(
                name: "actual_started_at",
                table: "meeting_sessions");
        }
    }
}
