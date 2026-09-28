using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NotificationManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "deduplication_key",
                table: "notifications",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "pushed_at",
                table: "notifications",
                type: "timestamp with time zone",
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

            // Rows that predate realtime delivery must remain in the inbox without being replayed
            // to every connected client the first time the worker starts after this migration.
            migrationBuilder.Sql("""
                UPDATE notifications
                SET pushed_at = created_at,
                    target_type = CASE WHEN meeting_id IS NOT NULL THEN 'Meeting' ELSE NULL END,
                    target_id = meeting_id;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_deduplication_key",
                table: "notifications",
                column: "deduplication_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_pushed_at_created_at",
                table: "notifications",
                columns: new[] { "pushed_at", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_notifications_deduplication_key",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_notifications_pushed_at_created_at",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "deduplication_key",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "pushed_at",
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
