using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GuestCallParticipants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "started_by_id",
                table: "call_sessions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "call_participants",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "guest_session_id",
                table: "call_participants",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_call_participants_guest_session_id",
                table: "call_participants",
                column: "guest_session_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_call_participants_one_member",
                table: "call_participants",
                sql: "(user_id IS NULL) <> (guest_session_id IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "fk_call_participants_guest_sessions_guest_session_id",
                table: "call_participants",
                column: "guest_session_id",
                principalTable: "guest_sessions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_call_participants_guest_sessions_guest_session_id",
                table: "call_participants");

            migrationBuilder.DropIndex(
                name: "ix_call_participants_guest_session_id",
                table: "call_participants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_call_participants_one_member",
                table: "call_participants");

            migrationBuilder.DropColumn(
                name: "guest_session_id",
                table: "call_participants");

            migrationBuilder.AlterColumn<Guid>(
                name: "started_by_id",
                table: "call_sessions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "call_participants",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
