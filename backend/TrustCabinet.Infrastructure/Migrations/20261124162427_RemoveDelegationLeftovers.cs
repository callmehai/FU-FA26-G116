using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDelegationLeftovers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // BR-046 (D4): invitation answers are only Accepted/Declined. Rows written before
            // delegation was dropped carried response 'Delegated'; fold them to 'Declined' so the
            // narrowed InvitationResponse enum keeps reading them after the column goes away.
            migrationBuilder.Sql(
                "UPDATE meeting_participants SET response = 'Declined' WHERE response = 'Delegated';");

            migrationBuilder.DropForeignKey(
                name: "fk_meeting_participants_users_delegated_to_user_id",
                table: "meeting_participants");

            migrationBuilder.DropIndex(
                name: "ix_meeting_participants_delegated_to_user_id",
                table: "meeting_participants");

            migrationBuilder.DropColumn(
                name: "delegated_to_user_id",
                table: "meeting_participants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "delegated_to_user_id",
                table: "meeting_participants",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_meeting_participants_delegated_to_user_id",
                table: "meeting_participants",
                column: "delegated_to_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_meeting_participants_users_delegated_to_user_id",
                table: "meeting_participants",
                column: "delegated_to_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
