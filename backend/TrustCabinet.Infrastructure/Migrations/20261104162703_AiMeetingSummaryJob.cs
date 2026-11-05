using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AiMeetingSummaryJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "edited_at",
                table: "meeting_summaries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "edited_by_id",
                table: "meeting_summaries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "error_message",
                table: "meeting_summaries",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "requested_at",
                table: "meeting_summaries",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "requested_by_id",
                table: "meeting_summaries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "meeting_summaries",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_summaries_edited_by_id",
                table: "meeting_summaries",
                column: "edited_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_summaries_requested_by_id",
                table: "meeting_summaries",
                column: "requested_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_summaries_status_requested_at",
                table: "meeting_summaries",
                columns: new[] { "status", "requested_at" });

            migrationBuilder.AddForeignKey(
                name: "fk_meeting_summaries_users_edited_by_id",
                table: "meeting_summaries",
                column: "edited_by_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_meeting_summaries_users_requested_by_id",
                table: "meeting_summaries",
                column: "requested_by_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_meeting_summaries_users_edited_by_id",
                table: "meeting_summaries");

            migrationBuilder.DropForeignKey(
                name: "fk_meeting_summaries_users_requested_by_id",
                table: "meeting_summaries");

            migrationBuilder.DropIndex(
                name: "ix_meeting_summaries_edited_by_id",
                table: "meeting_summaries");

            migrationBuilder.DropIndex(
                name: "ix_meeting_summaries_requested_by_id",
                table: "meeting_summaries");

            migrationBuilder.DropIndex(
                name: "ix_meeting_summaries_status_requested_at",
                table: "meeting_summaries");

            migrationBuilder.DropColumn(
                name: "edited_at",
                table: "meeting_summaries");

            migrationBuilder.DropColumn(
                name: "edited_by_id",
                table: "meeting_summaries");

            migrationBuilder.DropColumn(
                name: "error_message",
                table: "meeting_summaries");

            migrationBuilder.DropColumn(
                name: "requested_at",
                table: "meeting_summaries");

            migrationBuilder.DropColumn(
                name: "requested_by_id",
                table: "meeting_summaries");

            migrationBuilder.DropColumn(
                name: "status",
                table: "meeting_summaries");
        }
    }
}
