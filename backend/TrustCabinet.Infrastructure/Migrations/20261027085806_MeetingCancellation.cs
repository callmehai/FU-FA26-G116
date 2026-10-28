using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MeetingCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cancellation_reason",
                table: "meetings",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cancelled_at",
                table: "meetings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cancelled_by_id",
                table: "meetings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cancelled_from_status",
                table: "meetings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "actor_id",
                table: "meeting_status_history",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "ix_meetings_cancelled_by_id",
                table: "meetings",
                column: "cancelled_by_id");

            migrationBuilder.AddForeignKey(
                name: "fk_meetings_users_cancelled_by_id",
                table: "meetings",
                column: "cancelled_by_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A system-made history row and a Cancelled meeting have no representation in the old schema: refuse
            // rather than invent an actor or a status for them.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM meeting_status_history WHERE actor_id IS NULL)
                        OR EXISTS (SELECT 1 FROM meetings WHERE status = 'Cancelled') THEN
                        RAISE EXCEPTION 'Cancelled meetings exist; MeetingCancellation cannot be reverted without losing them.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_meetings_users_cancelled_by_id",
                table: "meetings");

            migrationBuilder.DropIndex(
                name: "ix_meetings_cancelled_by_id",
                table: "meetings");

            migrationBuilder.DropColumn(
                name: "cancellation_reason",
                table: "meetings");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                table: "meetings");

            migrationBuilder.DropColumn(
                name: "cancelled_by_id",
                table: "meetings");

            migrationBuilder.DropColumn(
                name: "cancelled_from_status",
                table: "meetings");

            migrationBuilder.AlterColumn<Guid>(
                name: "actor_id",
                table: "meeting_status_history",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
