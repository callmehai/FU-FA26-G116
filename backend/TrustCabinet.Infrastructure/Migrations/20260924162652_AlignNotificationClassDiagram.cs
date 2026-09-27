using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlignNotificationClassDiagram : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "action_url",
                table: "notifications",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "target_id",
                table: "notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "target_type",
                table: "notifications",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            // Existing meeting notifications must remain openable after adding explicit targets.
            migrationBuilder.Sql("""
                UPDATE notifications
                SET target_type = 'Meeting',
                    target_id = meeting_id,
                    action_url = '/mgmt/meetings/' || meeting_id::text
                WHERE meeting_id IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "action_url",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "target_id",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "target_type",
                table: "notifications");
        }
    }
}
