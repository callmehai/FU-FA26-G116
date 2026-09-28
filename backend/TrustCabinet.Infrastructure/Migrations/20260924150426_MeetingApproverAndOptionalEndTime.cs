using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MeetingApproverAndOptionalEndTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ends_at",
                table: "meetings",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<Guid>(
                name: "assigned_approver_id",
                table: "meetings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_meetings_assigned_approver_id",
                table: "meetings",
                column: "assigned_approver_id");

            migrationBuilder.AddForeignKey(
                name: "fk_meetings_users_assigned_approver_id",
                table: "meetings",
                column: "assigned_approver_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_meetings_users_assigned_approver_id",
                table: "meetings");

            migrationBuilder.DropIndex(
                name: "ix_meetings_assigned_approver_id",
                table: "meetings");

            migrationBuilder.DropColumn(
                name: "assigned_approver_id",
                table: "meetings");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ends_at",
                table: "meetings",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }
    }
}
