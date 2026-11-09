using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MinutesApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "returned_at",
                table: "meeting_minutes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "returned_by_id",
                table: "meeting_minutes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "submitted_by_id",
                table: "meeting_minutes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_meeting_minutes_returned_by_id",
                table: "meeting_minutes",
                column: "returned_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_minutes_submitted_by_id",
                table: "meeting_minutes",
                column: "submitted_by_id");

            migrationBuilder.AddForeignKey(
                name: "fk_meeting_minutes_users_returned_by_id",
                table: "meeting_minutes",
                column: "returned_by_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_meeting_minutes_users_submitted_by_id",
                table: "meeting_minutes",
                column: "submitted_by_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_meeting_minutes_users_returned_by_id",
                table: "meeting_minutes");

            migrationBuilder.DropForeignKey(
                name: "fk_meeting_minutes_users_submitted_by_id",
                table: "meeting_minutes");

            migrationBuilder.DropIndex(
                name: "ix_meeting_minutes_returned_by_id",
                table: "meeting_minutes");

            migrationBuilder.DropIndex(
                name: "ix_meeting_minutes_submitted_by_id",
                table: "meeting_minutes");

            migrationBuilder.DropColumn(
                name: "returned_at",
                table: "meeting_minutes");

            migrationBuilder.DropColumn(
                name: "returned_by_id",
                table: "meeting_minutes");

            migrationBuilder.DropColumn(
                name: "submitted_by_id",
                table: "meeting_minutes");
        }
    }
}
